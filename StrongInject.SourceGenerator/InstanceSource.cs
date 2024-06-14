using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using StrongInject.Generator.Visitors;

namespace StrongInject.Generator
{
    internal abstract record InstanceSource(Scope Scope, bool IsAsync, bool CanDecorate)
    {
        public abstract ITypeSymbol OfType { get; }

        public abstract void Visit<TState>(IVisitor<TState> visitor, TState state);
    }

    internal sealed record Registration(
        INamedTypeSymbol Type,
        Scope Scope,
        bool RequiresInitialization,
        IMethodSymbol Constructor,
        bool IsAsync) : InstanceSource(Scope, IsAsync, true)
    {
        public override ITypeSymbol OfType => Type;

        public bool Equals(Registration? other)
        {
            return other is not null && Scope == other.Scope && SymbolEqualityComparer.Default.Equals(Type, other.Type);
        }

        public override void Visit<TState>(IVisitor<TState> visitor, TState state)
        {
            visitor.Visit(this, state);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (SymbolEqualityComparer.Default.GetHashCode(Type) * -1521134295
                        + (int)Scope) * -1521134295;
            }
        }
    }

    internal sealed record FactorySource(ITypeSymbol FactoryOf, InstanceSource Underlying, Scope Scope, bool IsAsync)
        : InstanceSource(Scope, IsAsync, Underlying.CanDecorate)
    {
        public override ITypeSymbol OfType => FactoryOf;

        public bool Equals(FactorySource? other)
        {
            return other is not null && Scope == other.Scope &&
                   SymbolEqualityComparer.Default.Equals(FactoryOf, other.FactoryOf);
        }

        public override void Visit<TState>(IVisitor<TState> visitor, TState state)
        {
            visitor.Visit(this, state);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (SymbolEqualityComparer.Default.GetHashCode(FactoryOf) * -1521134295
                        + (int)Scope) * -1521134295;
            }
        }
    }

    internal sealed record DelegateSource(
        ITypeSymbol DelegateType,
        ITypeSymbol ReturnType,
        ImmutableArray<IParameterSymbol> Parameters,
        bool IsAsync) : InstanceSource(Scope.InstancePerResolution, IsAsync, true)
    {
        public override ITypeSymbol OfType => DelegateType;

        public bool Equals(DelegateSource? other)
        {
            return other is not null && SymbolEqualityComparer.Default.Equals(DelegateType, other.DelegateType);
        }

        public override void Visit<TState>(IVisitor<TState> visitor, TState state)
        {
            visitor.Visit(this, state);
        }

        public override int GetHashCode()
        {
            return SymbolEqualityComparer.Default.GetHashCode(DelegateType);
        }
    }

    internal sealed record DelegateParameter(IParameterSymbol Parameter, string Name, int Depth)
        : InstanceSource(Scope.InstancePerResolution, false, false)
    {
        public override ITypeSymbol OfType => Parameter.Type;

        public override void Visit<TState>(IVisitor<TState> visitor, TState state)
        {
            visitor.Visit(this, state);
        }
    }

    internal sealed record FactoryMethod(
        IMethodSymbol Method,
        ITypeSymbol FactoryOfType,
        Scope Scope,
        bool IsOpenGeneric,
        bool IsAsync) : InstanceSource(Scope, IsAsync, true)
    {
        public override ITypeSymbol OfType => FactoryOfType;

        public bool Equals(FactoryMethod? other)
        {
            return other is not null && Scope == other.Scope && SymbolEqualityComparer.Default.Equals(Method, other.Method);
        }

        public override void Visit<TState>(IVisitor<TState> visitor, TState state)
        {
            visitor.Visit(this, state);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (SymbolEqualityComparer.Default.GetHashCode(Method) * -1521134295
                        + (int)Scope) * -1521134295;
            }
        }
    }

    internal sealed record InstanceFieldOrProperty(ISymbol FieldOrPropertySymbol, ITypeSymbol Type)
        : InstanceSource(Scope.SingleInstance, false, true)
    {
        public override ITypeSymbol OfType => Type;

        public bool Equals(InstanceFieldOrProperty? other)
        {
            return other is not null &&
                   SymbolEqualityComparer.Default.Equals(FieldOrPropertySymbol, other.FieldOrPropertySymbol);
        }

        public override void Visit<TState>(IVisitor<TState> visitor, TState state)
        {
            visitor.Visit(this, state);
        }

        public override int GetHashCode()
        {
            return SymbolEqualityComparer.Default.GetHashCode(FieldOrPropertySymbol);
        }
    }

    internal sealed record ArraySource(
        IArrayTypeSymbol ArrayType,
        ITypeSymbol ElementType,
        IReadOnlyCollection<InstanceSource> Items) : InstanceSource(Scope.InstancePerDependency, false, true)
    {
        public override ITypeSymbol OfType => ArrayType;

        public bool Equals(ArraySource? other)
        {
            return other is not null && SymbolEqualityComparer.Default.Equals(ArrayType, other.ArrayType);
        }

        public override void Visit<TState>(IVisitor<TState> visitor, TState state)
        {
            visitor.Visit(this, state);
        }

        public override int GetHashCode()
        {
            return SymbolEqualityComparer.Default.GetHashCode(ArrayType);
        }
    }

    internal sealed record WrappedDecoratorInstanceSource(DecoratorSource Decorator, InstanceSource Underlying)
        : InstanceSource(Underlying.Scope, Decorator.IsAsync, true)
    {
        public override ITypeSymbol OfType => Decorator.OfType;

        public override void Visit<TState>(IVisitor<TState> visitor, TState state)
        {
            visitor.Visit(this, state);
        }
    }

    internal sealed record ForwardedInstanceSource : InstanceSource
    {
        private ForwardedInstanceSource(INamedTypeSymbol asType, InstanceSource underlying)
            : base(
                underlying.Scope,
                false,
                underlying.CanDecorate)
        {
            (AsType, Underlying) = (asType, underlying);
        }

        public INamedTypeSymbol AsType { get; init; }
        public InstanceSource Underlying { get; init; }

        public override ITypeSymbol OfType => AsType;

        public void Deconstruct(out INamedTypeSymbol AsType, out InstanceSource Underlying)
        {
            (AsType, Underlying) = (this.AsType, this.Underlying);
        }

        public static InstanceSource Create(INamedTypeSymbol asType, InstanceSource underlying)
        {
            return SymbolEqualityComparer.Default.Equals(underlying.OfType, asType)
                ? underlying
                : new ForwardedInstanceSource(
                    asType,
                    underlying is ForwardedInstanceSource forwardedUnderlying
                        ? forwardedUnderlying.Underlying
                        : underlying);
        }

        public override void Visit<TState>(IVisitor<TState> visitor, TState state)
        {
            visitor.Visit(this, state);
        }
    }

    internal sealed record OwnedSource(
        ITypeSymbol OwnedType,
        ITypeSymbol OwnedValueType,
        bool IsAsync) : InstanceSource(Scope.InstancePerDependency, IsAsync, true)
    {
        public override ITypeSymbol OfType => OwnedType;

        public bool Equals(OwnedSource? other)
        {
            return other is not null && SymbolEqualityComparer.Default.Equals(OwnedType, other.OwnedType);
        }

        public override void Visit<TState>(IVisitor<TState> visitor, TState state)
        {
            visitor.Visit(this, state);
        }

        public override int GetHashCode()
        {
            return SymbolEqualityComparer.Default.GetHashCode(OwnedType);
        }
    }
}