using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using static StrongInject.Generator.GenericResolutionHelpers;

namespace StrongInject.Generator
{
    internal class GenericDecoratorsResolver
    {
        private readonly List<DecoratorSource> _arrayDecoratorSources;
        private readonly Compilation _compilation;
        private readonly Dictionary<INamedTypeSymbol, List<DecoratorSource>> _namedTypeDecoratorSources;
        private readonly List<DecoratorSource> _typeParameterDecoratorSources;

        public GenericDecoratorsResolver(Compilation compilation, IEnumerable<DecoratorSource> decoratorFactoryMethods)
        {
            _namedTypeDecoratorSources = new Dictionary<INamedTypeSymbol, List<DecoratorSource>>();
            _arrayDecoratorSources = new List<DecoratorSource>();
            _typeParameterDecoratorSources = new List<DecoratorSource>();

            foreach (var decoratorFactoryMethod in decoratorFactoryMethods)
            {
                var list = decoratorFactoryMethod.OfType switch
                {
                    INamedTypeSymbol namedTypeSymbol => _namedTypeDecoratorSources.GetOrCreate(
                        namedTypeSymbol.OriginalDefinition,
                        _ => new List<DecoratorSource>()),
                    IArrayTypeSymbol => _arrayDecoratorSources,
                    ITypeParameterSymbol => _typeParameterDecoratorSources,
                    var typeSymbol => throw new InvalidOperationException($"Unexpected TypeSymbol {typeSymbol}"),
                };

                list.Add(decoratorFactoryMethod);
            }

            _compilation = compilation;
        }

        public ResolveDecoratorsEnumerable ResolveDecorators(ITypeSymbol type)
        {
            return new ResolveDecoratorsEnumerable(
                _compilation,
                _namedTypeDecoratorSources,
                _arrayDecoratorSources,
                _typeParameterDecoratorSources,
                type);
        }

        private static bool CanConstructFromGenericFactoryMethod(
            Compilation compilation,
            ITypeSymbol toConstruct,
            DecoratorFactoryMethod factoryMethod,
            out DecoratorFactoryMethod constructedFactoryMethod)
        {
            if (!CanConstructFromGenericMethodReturnType(
                    compilation,
                    toConstruct,
                    factoryMethod.DecoratedType,
                    factoryMethod.Method,
                    out var constructedMethod,
                    out _))
            {
                constructedFactoryMethod = null!;
                return false;
            }

            constructedFactoryMethod = factoryMethod with
            {
                DecoratedType = toConstruct,
                Method = constructedMethod,
                IsOpenGeneric = false,
            };

            return true;
        }

        public readonly struct ResolveDecoratorsEnumerable
        {
            private readonly Compilation _compilation;
            private readonly Dictionary<INamedTypeSymbol, List<DecoratorSource>> _namedTypeDecoratorSources;
            private readonly List<DecoratorSource> _arrayDecoratorSources;
            private readonly List<DecoratorSource> _typeParameterDecoratorSources;
            private readonly ITypeSymbol _type;

            public ResolveDecoratorsEnumerable(
                Compilation compilation,
                Dictionary<INamedTypeSymbol, List<DecoratorSource>> namedTypeDecoratorSources,
                List<DecoratorSource> arrayDecoratorSources,
                List<DecoratorSource> typeParameterDecoratorSources,
                ITypeSymbol type)
            {
                _compilation = compilation;
                _namedTypeDecoratorSources = namedTypeDecoratorSources;
                _arrayDecoratorSources = arrayDecoratorSources;
                _typeParameterDecoratorSources = typeParameterDecoratorSources;
                _type = type;
            }

            public ResolveDecoratorsEnumerator GetEnumerator()
            {
                return new ResolveDecoratorsEnumerator(
                    _compilation,
                    _namedTypeDecoratorSources,
                    _arrayDecoratorSources,
                    _typeParameterDecoratorSources,
                    _type);
            }
        }

        public struct ResolveDecoratorsEnumerator
        {
            private static readonly List<DecoratorSource> _emptyDecoratorSources = new();

            private readonly Compilation _compilation;
            private readonly ITypeSymbol _type;

            private List<DecoratorSource>.Enumerator _decoratorSources;
            private List<DecoratorSource>.Enumerator _arrayDecoratorSources;
            private List<DecoratorSource>.Enumerator _typeParameterDecoratorSources;

            public ResolveDecoratorsEnumerator(
                Compilation compilation,
                Dictionary<INamedTypeSymbol, List<DecoratorSource>> namedTypeDecoratorSources,
                List<DecoratorSource> arrayDecoratorSources,
                List<DecoratorSource> typeParameterDecoratorSources,
                ITypeSymbol type)
            {
                _compilation = compilation;
                _type = type;

                _arrayDecoratorSources = arrayDecoratorSources.GetEnumerator();
                _typeParameterDecoratorSources = typeParameterDecoratorSources.GetEnumerator();

                if (type is INamedTypeSymbol namedType
                    && namedTypeDecoratorSources.TryGetValue(namedType.OriginalDefinition, out var decoratorSources))
                    _decoratorSources = decoratorSources.GetEnumerator();
                else
                    _decoratorSources = _emptyDecoratorSources.GetEnumerator();

                Current = null!;
            }

            public DecoratorSource Current { get; private set; }

            public bool MoveNext()
            {
                if (_type is INamedTypeSymbol namedType)
                    while (_decoratorSources.MoveNext())
                        switch (_decoratorSources.Current)
                        {
                            case DecoratorFactoryMethod decoratorFactoryMethod:
                                if (CanConstructFromGenericFactoryMethod(
                                        _compilation,
                                        _type,
                                        decoratorFactoryMethod,
                                        out var constructedFactoryMethod))
                                {
                                    Current = constructedFactoryMethod;
                                    return true;
                                }

                                break;

                            case DecoratorRegistration decoratorRegistration:
                                var constructed = decoratorRegistration.Type.Construct(namedType.TypeArguments.ToArray());
                                var originalConstructor = decoratorRegistration.Constructor;
                                var constructor = constructed.InstanceConstructors.First(
                                    x => x.OriginalDefinition.Equals(originalConstructor));

                                Current = decoratorRegistration with
                                {
                                    Constructor = constructor,
                                    Type = constructed,
                                    DecoratedType = namedType,
                                };

                                return true;

                            case var source:
                                throw new NotSupportedException(source.ToString());
                        }
                else if (_type is IArrayTypeSymbol)
                    while (_arrayDecoratorSources.MoveNext())
                        if (CanConstructFromGenericFactoryMethod(
                                _compilation,
                                _type,
                                // ReSharper disable once AssignNullToNotNullAttribute
                                (DecoratorFactoryMethod)_arrayDecoratorSources.Current,
                                out var constructedFactoryMethod))
                        {
                            Current = constructedFactoryMethod;
                            return true;
                        }

                while (_typeParameterDecoratorSources.MoveNext())
                    if (CanConstructFromGenericFactoryMethod(
                            _compilation,
                            _type,
                            // ReSharper disable once AssignNullToNotNullAttribute
                            (DecoratorFactoryMethod)_typeParameterDecoratorSources.Current,
                            out var constructedFactoryMethod))
                    {
                        Current = constructedFactoryMethod;
                        return true;
                    }

                return false;
            }
        }
    }
}