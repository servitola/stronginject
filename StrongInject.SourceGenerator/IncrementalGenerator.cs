using System;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StrongInject.Generator
{
    [Generator]
    internal class IncrementalGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var trees = context.SyntaxProvider.CreateSyntaxProvider(
                (node, _) =>
                {
                    if (node is not ClassDeclarationSyntax
                        {
                            BaseList: var baseList,
                            AttributeLists: var attributes,
                            Members: var members,
                        })
                        return false;

                    if (baseList is not null)
                        foreach (var type in baseList.Types)
                            if (type.Type is NameSyntax name && WellKnownTypes.IsContainerCandidate(name))
                                return true;

                    foreach (var attributeList in attributes)
                    foreach (var attribute in attributeList.Attributes)
                        if (WellKnownTypes.IsClassAttributeCandidate(attribute.Name))
                            return true;

                    foreach (var member in members)
                    foreach (var attributeList in member.AttributeLists)
                    foreach (var attribute in attributeList.Attributes)
                        if (WellKnownTypes.IsMemberAttributeCandidate(attribute.Name))
                            return true;

                    return false;
                },
                (ctx, cancellationToken) =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (ctx.SemanticModel.GetDeclaredSymbol(ctx.Node, cancellationToken) is not INamedTypeSymbol type)
                        return default;

                    var isContainer = type.AllInterfaces.Any(x => WellKnownTypes.IsContainerOrAsyncContainer(x));

                    cancellationToken.ThrowIfCancellationRequested();
                    if (!isContainer
                        && !type.GetAttributes().Any(x => WellKnownTypes.IsClassAttribute(x.AttributeClass))
                        && !type.GetMembers()
                            .Any(
                                x =>
                                {
                                    if (x is IFieldSymbol or IPropertySymbol &&
                                        x.GetAttributes().Any(x => WellKnownTypes.IsInstanceAttribute(x.AttributeClass)))
                                        return true;

                                    return x is IMethodSymbol &&
                                           x.GetAttributes().Any(x => WellKnownTypes.IsMethodAttribute(x.AttributeClass));
                                }))
                        return default;

                    return (isContainer, ctx.Node);
                });

            var compilationWrapper = context.CompilationProvider.Select((x, _) => new CompilationWrapper(x));

            context.RegisterSourceOutput(
                trees.Combine(compilationWrapper),
                (context, x) =>
                {
                    var (isContainer, node) = x.Left;
                    if (node is null)
                        return;

                    var compilation = x.Right.Compilation;
                    var cancellationToken = context.CancellationToken;
                    var reportDiagnostic = context.ReportDiagnostic;
                    if (compilation.GetSemanticModel(node.SyntaxTree).GetDeclaredSymbol(node, cancellationToken) is not
                        INamedTypeSymbol type)
                        throw new InvalidOperationException(node.ToString());

                    if (!type.IsInternal() && !type.IsPublic())
                        reportDiagnostic(
                            ModuleNotPublicOrInternal(
                                type,
                                ((TypeDeclarationSyntax)node).Identifier
                                .GetLocation()));

                    cancellationToken.ThrowIfCancellationRequested();
                    if (!WellKnownTypes.TryCreate(compilation, reportDiagnostic, out var wellKnownTypes))
                        return;

                    var registrationCalculator = new RegistrationCalculator(compilation, wellKnownTypes, cancellationToken);
                    if (!isContainer)
                    {
                        registrationCalculator.ValidateModuleRegistrations(type, reportDiagnostic);
                        return;
                    }

                    var file = ContainerGenerator.GenerateContainerImplementations(
                        type,
                        registrationCalculator.GetContainerRegistrations(type, reportDiagnostic),
                        wellKnownTypes,
                        reportDiagnostic,
                        cancellationToken)
                        .Replace("global::", "")
                        .Replace("private ", "");

                    context.AddSource(GenerateNameHint(type), file);
                });
        }

        private string GenerateNameHint(INamedTypeSymbol container)
        {
            var stringBuilder = new StringBuilder(container.ContainingNamespace.FullName());
            foreach (var type in container.GetContainingTypesAndThis().Reverse())
            {
                stringBuilder.Append(".");
                stringBuilder.Append(type.Name);
                if (type.TypeParameters.Length > 0)
                {
                    stringBuilder.Append("_");
                    stringBuilder.Append(type.TypeParameters.Length);
                }
            }

            stringBuilder.Append(".g.cs");
            return stringBuilder.ToString();
        }

        private Diagnostic ModuleNotPublicOrInternal(ITypeSymbol module, Location location)
        {
            return Diagnostic.Create(
                new DiagnosticDescriptor(
                    "SI0401",
                    "Module must be public or internal.",
                    "Module '{0}' must be public or internal.",
                    "StrongInject",
                    DiagnosticSeverity.Error,
                    true),
                location,
                module.ToDisplayString());
        }

        private class CompilationWrapper : IEquatable<CompilationWrapper>
        {
            // We need to lock both this and other for Equals, which is difficult to do without risking a deadlock.
            // Given how rarely Equals is likely to be called, just use a shared lock among all instances.
            private static readonly object _lock = new();
            private static long _nextVersion;
            private Compilation _compilation;

            private long _version;

            public CompilationWrapper(Compilation compilation)
            {
                _compilation = compilation;
                _version = Interlocked.Increment(ref _nextVersion);
            }

            public Compilation Compilation
            {
                get
                {
                    lock (_lock)
                    {
                        return _compilation;
                    }
                }
            }

            public bool Equals(CompilationWrapper? other)
            {
                if (other is null)
                    return false;

                lock (_lock)
                {
                    if (other._version > _version)
                        (_compilation, _version) = (other._compilation, other._version);
                    else
                        (other._compilation, other._version) = (_compilation, _version);
                }

                return true;
            }

            public override bool Equals(object? obj)
            {
                return Equals(obj as CompilationWrapper);
            }

            public override int GetHashCode()
            {
                return 0;
            }
        }
    }
}