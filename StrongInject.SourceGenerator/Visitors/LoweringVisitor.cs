﻿using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace StrongInject.Generator.Visitors
{
    internal class LoweringVisitor : BaseVisitor<LoweringVisitor.State>
    {
        private static readonly List<Operation> _emptyList = new();
        private readonly InstanceSourcesScope _containerScope;
        private readonly Dictionary<DelegateSource, string> _currentlyVisitingDelegates;
        private readonly DisposalLowerer _disposalLowerer;
        private readonly Dictionary<InstanceSource, (Operation operation, string name)> _existingVariables = new();
        private readonly bool _isAsyncContext;
        private readonly bool _isSingleInstanceCreation;
        private readonly List<Operation> _order = new();
        private readonly RequiresAsyncChecker _requiresAsyncChecker;
        private readonly InstanceSource _target;
        private int _variableCount;

        private LoweringVisitor(
            RequiresAsyncChecker requiresAsyncChecker,
            InstanceSource target,
            InstanceSourcesScope containerScope,
            DisposalLowerer disposalLowerer,
            bool isSingleInstanceCreation,
            bool isAsyncContext,
            IEnumerable<KeyValuePair<InstanceSource, (Operation operation, string name)>>? singleInstanceVariablesInScope,
            Dictionary<DelegateSource, string> currentlyVisitingDelegates,
            CancellationToken cancellationToken)
            : base(cancellationToken)
        {
            if (singleInstanceVariablesInScope != null)
                foreach (var (source, value) in singleInstanceVariablesInScope)
                    _existingVariables.Add(source, value);

            _requiresAsyncChecker = requiresAsyncChecker;
            _target = target;
            _containerScope = containerScope;
            _disposalLowerer = disposalLowerer;
            _isSingleInstanceCreation = isSingleInstanceCreation;
            _isAsyncContext = isAsyncContext;
            _currentlyVisitingDelegates = currentlyVisitingDelegates;
        }

        public static ImmutableArray<Operation> LowerResolution(
            RequiresAsyncChecker requiresAsyncChecker,
            InstanceSource target,
            InstanceSourcesScope containerScope,
            DisposalLowerer disposalLowerer,
            bool isSingleInstanceCreation,
            bool isAsyncContext,
            out string targetName,
            CancellationToken cancellationToken)
        {
            return LowerResolution(
                requiresAsyncChecker,
                target,
                containerScope,
                containerScope,
                disposalLowerer,
                isSingleInstanceCreation,
                isAsyncContext,
                null,
                new Dictionary<DelegateSource, string>(),
                out targetName,
                cancellationToken);
        }

        private static ImmutableArray<Operation> LowerResolution(
            RequiresAsyncChecker requiresAsyncChecker,
            InstanceSource target,
            InstanceSourcesScope currentScope,
            InstanceSourcesScope containerScope,
            DisposalLowerer disposalLowerer,
            bool isSingleInstanceCreation,
            bool isAsyncContext,
            IEnumerable<KeyValuePair<InstanceSource, (Operation operation, string name)>>? singleInstanceVariablesInScope,
            Dictionary<DelegateSource, string> currentlyVisitingDelegates,
            out string targetName,
            CancellationToken cancellationToken)
        {
            var visitor = new LoweringVisitor(
                requiresAsyncChecker,
                target,
                containerScope,
                disposalLowerer,
                isSingleInstanceCreation,
                isAsyncContext,
                singleInstanceVariablesInScope,
                currentlyVisitingDelegates,
                cancellationToken);

            var state = new State
            {
                InstanceSourcesScope = currentScope, Dependencies = new List<string?>(),
                OperationDependencies = new List<Operation>(),
            };

            visitor.VisitCore(target, state);
            targetName = state.Dependencies[0]!;
            return isAsyncContext ? Order(visitor._order) : visitor._order.ToImmutableArray();
        }

        protected override bool ShouldVisitBeforeUpdateState(InstanceSource? source, State state)
        {
            if (source is null)
            {
                // Add an entry to mark an optional dependency that isn't provided.
                state.Dependencies.Add(null);
                return false;
            }

            if (source.Scope is not Scope.InstancePerDependency && _existingVariables.TryGetValue(source, out var value))
            {
                state.Dependencies.Add(value.name);
                state.OperationDependencies.Add(value.operation);
                return false;
            }

            if (source is DelegateSource ds && _currentlyVisitingDelegates.TryGetValue(ds, out var name))
            {
                state.Dependencies.Add(name);
                return false;
            }

            if (source is { Scope: Scope.SingleInstance } and not (InstanceFieldOrProperty or ForwardedInstanceSource) &&
                !(ReferenceEquals(_target, source) && _isSingleInstanceCreation))
            {
                name = GenerateName(source, state);
                var isAsync = _requiresAsyncChecker.RequiresAsync(source);
                var statement = new SingleInstanceReferenceStatement(name, source, isAsync);
                Operation targetOperation;
                if (isAsync)
                {
                    var awaitVariableName = GenerateName(source, state);
                    var awaitStatement = new AwaitStatement(awaitVariableName, name, source.OfType);
                    var referenceOperation = CreateOperation(statement, state.Name, _emptyList, awaitStatement);
                    _order.Add(referenceOperation);
                    targetOperation =
                        CreateOperation(awaitStatement, state.Name, new List<Operation> { referenceOperation });

                    _order.Add(targetOperation);
                    name = awaitVariableName;
                }
                else
                {
                    targetOperation = CreateOperation(statement, state.Name, _emptyList);
                    _order.Add(targetOperation);
                }

                _existingVariables.Add(source, (targetOperation, name));
                state.Dependencies.Add(name);
                state.OperationDependencies.Add(targetOperation);
                return false;
            }

            if (source is DelegateParameter { Name: var variableName })
            {
                state.Dependencies.Add(variableName);
                return false;
            }

            return true;
        }

        private string GenerateName(InstanceSource source, State state)
        {
            return source.OfType.ToLowerCaseIdentifier("") + "_" + state.InstanceSourcesScope.Depth + "_" +
                   _variableCount++;
        }

        protected override void UpdateState(InstanceSource source, ref State state)
        {
            state.Name = GenerateName(source, state);
            state.Dependencies.Add(state.Name);
            state.Dependencies = new List<string?>();
            state.OperationDependencies = new List<Operation>();
            base.UpdateState(source, ref state);
        }

        protected override bool ShouldVisitAfterUpdateState(InstanceSource source, State state)
        {
            return true;
        }

        protected override void AfterVisit(InstanceSource source, State state)
        {
            Operation targetOperation;
            if (source is DelegateSource delegateSource)
            {
                _currentlyVisitingDelegates.Add(delegateSource, state.Name);
                var order = LowerResolution(
                    _requiresAsyncChecker,
                    GetInstanceSource(delegateSource.ReturnType, state, null)!,
                    state.InstanceSourcesScope,
                    _containerScope,
                    _disposalLowerer,
                    false,
                    delegateSource.IsAsync,
                    _existingVariables.Where(x => x.Key.Scope == Scope.SingleInstance),
                    _currentlyVisitingDelegates,
                    out var targetName,
                    _cancellationToken);

                _currentlyVisitingDelegates.Remove(delegateSource);
                var delegateCreationStatement =
                    new DelegateCreationStatement(state.Name, delegateSource, order, targetName);

                targetOperation = CreateOperation(delegateCreationStatement, state.Name, state.OperationDependencies);
                if (targetOperation.Disposal is Disposal.DelegateDisposal
                    {
                        DisposeActionsTypeName: var disposeActionsType,
                    })
                {
                    var disposeActionsOperation =
                        CreateOperation(
                            new DisposeActionsCreationStatement(
                                delegateCreationStatement.DisposeActionsName,
                                disposeActionsType),
                            state.Name,
                            _emptyList);

                    state.OperationDependencies.Add(disposeActionsOperation);
                    _order.Add(disposeActionsOperation);
                }

                _order.Add(targetOperation);
            }
            else if (source is OwnedSource ownedSource)
            {
                var internalTarget = GetInstanceSource(ownedSource.OwnedValueType, state, null)!;
                var internalOperations = LowerResolution(
                    _requiresAsyncChecker,
                    internalTarget,
                    state.InstanceSourcesScope,
                    _containerScope,
                    _disposalLowerer.WithDisposalStyle(
                        new DisposalStyle(
                            ownedSource.IsAsync,
                            DisposalStyleDeterminant.OwnedType)),
                    false,
                    ownedSource.IsAsync,
                    _existingVariables.Where(x => x.Key.Scope == Scope.SingleInstance),
                    _currentlyVisitingDelegates,
                    out var internalTargetName,
                    _cancellationToken);

                var localFunctionName = _existingVariables.GetValueOrDefault(source).name;
                if (localFunctionName is null)
                {
                    localFunctionName = "Create" + ownedSource.OwnedType.Name + ownedSource.OwnedValueType.Name + "_" +
                                        _variableCount++;

                    var localFunctionStatementOperation = new Operation(
                        new OwnedCreationLocalFunctionStatement(
                            ownedSource,
                            _isAsyncContext,
                            localFunctionName,
                            internalOperations,
                            internalTargetName),
                        null,
                        _emptyList,
                        false);

                    _order.Add(localFunctionStatementOperation);
                    _existingVariables.Add(source, (localFunctionStatementOperation, localFunctionName));
                }

                var ownedCreationStatement =
                    new OwnedCreationStatement(state.Name, ownedSource, _isAsyncContext, localFunctionName);

                targetOperation = CreateOperation(ownedCreationStatement, state.Name, state.OperationDependencies);
                _order.Add(targetOperation);
            }
            else
            {
                var requiresInitialization = source is
                    Registration { RequiresInitialization: true }
                    or WrappedDecoratorInstanceSource { Decorator: DecoratorRegistration { RequiresInitialization: true } };

                if (requiresInitialization)
                {
                    var operation = CreateOperation(
                        new DependencyCreationStatement(
                            state.Name,
                            source,
                            state.Dependencies.ToImmutableArray()),
                        state.Name,
                        state.OperationDependencies);

                    _order.Add(operation);

                    var initializationTaskVariableName = source.IsAsync ? GenerateName(source, state) : null;

                    if (initializationTaskVariableName is not null)
                    {
                        var awaitStatement = new AwaitStatement(null, initializationTaskVariableName, null);
                        var initializationOperation =
                            CreateOperation(
                                new InitializationStatement(initializationTaskVariableName, state.Name, source.IsAsync),
                                state.Name,
                                new List<Operation> { operation },
                                awaitStatement);

                        _order.Add(initializationOperation);
                        targetOperation = CreateOperation(
                            awaitStatement,
                            state.Name,
                            new List<Operation> { initializationOperation });

                        _order.Add(targetOperation);
                    }
                    else
                    {
                        targetOperation =
                            CreateOperation(
                                new InitializationStatement(initializationTaskVariableName, state.Name, source.IsAsync),
                                state.Name,
                                new List<Operation> { operation });

                        _order.Add(targetOperation);
                    }
                }
                else if (source.IsAsync)
                {
                    var taskVariableName = GenerateName(source, state);
                    var awaitStatement = new AwaitStatement(state.Name, taskVariableName, source.OfType);
                    var operation = CreateOperation(
                        new DependencyCreationStatement(
                            taskVariableName,
                            source,
                            state.Dependencies.ToImmutableArray()),
                        state.Name,
                        state.OperationDependencies,
                        awaitStatement);

                    _order.Add(operation);
                    targetOperation = CreateOperation(awaitStatement, state.Name, new List<Operation> { operation });
                    _order.Add(targetOperation);
                }
                else
                {
                    targetOperation = CreateOperation(
                        new DependencyCreationStatement(
                            state.Name,
                            source,
                            state.Dependencies.ToImmutableArray()),
                        state.Name,
                        state.OperationDependencies);

                    _order.Add(targetOperation);
                }
            }

            if (source.Scope != Scope.InstancePerDependency)
                _existingVariables.Add(source, (targetOperation, state.Name));

            state.ParentOperationDependencies.Add(targetOperation);
        }

        public override void Visit(DelegateSource delegateSource, State state)
        {
            if (_isAsyncContext && !delegateSource.IsAsync)
            {
                state.InstanceSourcesScope = state.PreviousScope!;
                foreach (var dependency in SingleInstanceVariablesToCreateEarlyVisitor.CalculateVariables(
                             _requiresAsyncChecker,
                             delegateSource,
                             state.InstanceSourcesScope,
                             _containerScope,
                             _cancellationToken))
                    VisitCore(dependency, state);
            }
        }

        public override void Visit(OwnedSource ownedSource, State state)
        {
            // OwnedSource is already about to be handled in AfterVisit like DelegateSource.
            // Unlike DelegateSource, there is nothing to do until then.
        }

        private Operation CreateOperation(
            Statement statement,
            string variableToDisposeName,
            List<Operation> dependencies,
            AwaitStatement? awaitStatement = null)
        {
            var disposal = _disposalLowerer.CreateDisposal(statement, variableToDisposeName);
            var canDisposeLocally = disposal is not null && (!disposal.IsAsync || _isAsyncContext);
            return new Operation(statement, disposal, dependencies, canDisposeLocally, awaitStatement);
        }

        private static ImmutableArray<Operation> Order(List<Operation> operations)
        {
            var builder = ImmutableArray.CreateBuilder<Operation>(operations.Count);
            var ordered = new HashSet<Operation>();
            while (operations.Count > 0)
            {
                for (var i = 0; i < operations.Count; i++)
                {
                    var operation = operations[i];

                    if (operation.Statement is InitializationStatement { IsAsync: true }
                        or SingleInstanceReferenceStatement { IsAsync: true }
                        or DependencyCreationStatement
                        {
                            Source:
                            FactoryMethod { IsAsync: true }
                            or WrappedDecoratorInstanceSource { Decorator: DecoratorFactoryMethod { IsAsync: true } }
                            or FactorySource { IsAsync: true },
                        })
                        if (operation.Dependencies.All(x => ordered.Contains(x)))
                        {
                            builder.Add(operation);
                            ordered.Add(operation);
                            operations.RemoveAt(i);
                            i--;
                        }
                }

                var found = false;
                for (var i = 0; i < operations.Count; i++)
                {
                    var operation = operations[i];

                    if (operation.Statement is not AwaitStatement && operation.Dependencies.All(x => ordered.Contains(x)))
                    {
                        builder.Add(operation);
                        ordered.Add(operation);
                        operations.RemoveAt(i);
                        found = true;
                        break;
                    }
                }

                if (found)
                    continue;

                Operation longestPathSoFar = default!;
                var longestPathLengthSoFar = 0;
                FindLongestPath(operations[operations.Count - 1], 0);
                builder.Add(longestPathSoFar);
                ordered.Add(longestPathSoFar);
                operations.Remove(longestPathSoFar);

                void FindLongestPath(Operation operation, int pathLength)
                {
                    if (operation.Statement is AwaitStatement)
                    {
                        pathLength++;
                        if (pathLength > longestPathLengthSoFar)
                        {
                            longestPathSoFar = operation;
                            longestPathLengthSoFar = pathLength;
                        }
                    }

                    foreach (var dependency in operation.Dependencies)
                        if (!ordered.Contains(dependency))
                            FindLongestPath(dependency, pathLength);
                }
            }

            return builder.MoveToImmutable();
        }

        public struct State : IState
        {
            private InstanceSourcesScope _instanceSourcesScope;
            private List<Operation> _operationDependencies;

            public InstanceSourcesScope? PreviousScope { get; private set; }

            public InstanceSourcesScope InstanceSourcesScope
            {
                get => _instanceSourcesScope;
                set
                {
                    PreviousScope = _instanceSourcesScope;
                    _instanceSourcesScope = value;
                }
            }

            public string Name { get; set; }
            public List<string?> Dependencies { get; set; }
            public List<Operation> ParentOperationDependencies { get; private set; }

            public List<Operation> OperationDependencies
            {
                get => _operationDependencies;
                set
                {
                    ParentOperationDependencies = _operationDependencies;
                    _operationDependencies = value;
                }
            }
        }
    }
}