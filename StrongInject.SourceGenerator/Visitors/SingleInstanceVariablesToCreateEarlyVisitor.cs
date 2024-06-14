using System.Collections.Generic;
using System.Threading;

namespace StrongInject.Generator.Visitors
{
    internal class SingleInstanceVariablesToCreateEarlyVisitor : SimpleVisitor
    {
        private readonly RequiresAsyncChecker _requiresAsyncChecker;
        private readonly List<InstanceSource> _singleInstanceVariablesToCreateEarly = new();

        private SingleInstanceVariablesToCreateEarlyVisitor(
            RequiresAsyncChecker requiresAsyncChecker,
            InstanceSourcesScope containerScope,
            CancellationToken cancellationToken)
            : base(
                containerScope,
                cancellationToken)
        {
            _requiresAsyncChecker = requiresAsyncChecker;
        }

        public static List<InstanceSource> CalculateVariables(
            RequiresAsyncChecker requiresAsyncChecker,
            InstanceSource source,
            InstanceSourcesScope currentScope,
            InstanceSourcesScope containerScope,
            CancellationToken cancellationToken)
        {
            var visitor =
                new SingleInstanceVariablesToCreateEarlyVisitor(requiresAsyncChecker, containerScope, cancellationToken);

            visitor.VisitCore(source, new State(currentScope));
            return visitor._singleInstanceVariablesToCreateEarly;
        }

        protected override bool ShouldVisitBeforeUpdateState(InstanceSource? source, State state)
        {
            if (source is null)
                return false;

            if (source is DelegateSource { IsAsync: true })
                return false;

            if (source.Scope == Scope.SingleInstance)
            {
                if (_requiresAsyncChecker.RequiresAsync(source))
                    _singleInstanceVariablesToCreateEarly.Add(source);

                return false;
            }

            return base.ShouldVisitBeforeUpdateState(source, state);
        }
    }
}