using System;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace StrongInject.Generator.Visitors
{
    internal abstract class BaseVisitor<TState> : IVisitor<TState>
        where TState : struct, BaseVisitor<TState>.IState
    {
        protected readonly CancellationToken _cancellationToken;

        private bool _exitFast;

        protected BaseVisitor(CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;
        }

        public virtual void Visit(Registration registration, TState state)
        {
            foreach (var param in registration.Constructor.Parameters)
            {
                if (_exitFast)
                    return;

                VisitCore(GetInstanceSource(param.Type, state, param), state);
            }
        }

        public virtual void Visit(FactorySource factorySource, TState state)
        {
            VisitCore(factorySource.Underlying, state);
        }

        public virtual void Visit(DelegateSource delegateSource, TState state)
        {
            VisitCore(GetInstanceSource(delegateSource.ReturnType, state, null), state);
        }

        public virtual void Visit(DelegateParameter delegateParameter, TState state)
        {
        }

        public virtual void Visit(FactoryMethod factoryMethod, TState state)
        {
            foreach (var param in factoryMethod.Method.Parameters)
            {
                if (_exitFast)
                    return;

                VisitCore(GetInstanceSource(param.Type, state, param), state);
            }
        }

        public virtual void Visit(InstanceFieldOrProperty instanceFieldOrProperty, TState state)
        {
        }

        public virtual void Visit(ArraySource arraySource, TState state)
        {
            foreach (var item in arraySource.Items)
            {
                if (_exitFast)
                    return;

                VisitCore(item, state);
            }
        }

        public virtual void Visit(WrappedDecoratorInstanceSource wrappedDecoratorInstanceSource, TState state)
        {
            var parameters = wrappedDecoratorInstanceSource.Decorator switch
            {
                DecoratorRegistration { Constructor: { Parameters: var prms } } => prms,
                DecoratorFactoryMethod { Method: { Parameters: var prms } } => prms,
                var decoratorSource => throw new NotSupportedException(decoratorSource.GetType().ToString()),
            };

            var decoratedParameterOrdinal = wrappedDecoratorInstanceSource.Decorator.DecoratedParameter;
            foreach (var param in parameters)
            {
                if (_exitFast)
                    return;

                var paramSource = param.Ordinal == decoratedParameterOrdinal
                    ? wrappedDecoratorInstanceSource.Underlying
                    : GetInstanceSource(param.Type, state, param);

                VisitCore(paramSource, state);
            }
        }

        public virtual void Visit(ForwardedInstanceSource forwardedInstanceSource, TState state)
        {
            VisitCore(forwardedInstanceSource.Underlying, state);
        }

        public virtual void Visit(OwnedSource ownedSource, TState state)
        {
            VisitCore(GetInstanceSource(ownedSource.OwnedValueType, state, null), state);
        }

        protected void ExitFast()
        {
            _exitFast = true;
        }

        public void VisitCore(InstanceSource? source, TState state)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            if (!_exitFast && ShouldVisitBeforeUpdateState(source, state) && source is not null)
            {
                UpdateState(source, ref state);
                if (ShouldVisitAfterUpdateState(source, state))
                {
                    source.Visit(this, state);
                    AfterVisit(source, state);
                }
            }
        }

        protected virtual void UpdateState(InstanceSource source, ref TState state)
        {
            state.InstanceSourcesScope = state.InstanceSourcesScope.Enter(source);
        }

        protected abstract bool ShouldVisitBeforeUpdateState(InstanceSource? source, TState state);

        protected abstract bool ShouldVisitAfterUpdateState(InstanceSource source, TState state);

        protected virtual void AfterVisit(InstanceSource source, TState state)
        {
        }

        protected virtual InstanceSource? GetInstanceSource(
            ITypeSymbol type,
            TState state,
            IParameterSymbol? parameterSymbol)
        {
            if (parameterSymbol is not null)
                return state.InstanceSourcesScope.GetParameterSource(parameterSymbol);

            return state.InstanceSourcesScope[type];
        }

        public interface IState
        {
            InstanceSourcesScope InstanceSourcesScope { get; set; }
        }
    }
}