﻿using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using StrongInject;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace StrongInject.Generator.Tests.Unit
{
    public class DependencyCheckerTests : TestBase
    {
        public DependencyCheckerTests(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }

        [Fact]
        public void NoErrorForCorrectDependencies()
        {
            string userSource = @"
using StrongInject;

[Registration(typeof(A))]
[Registration(typeof(B))]
[Registration(typeof(C))]
[Registration(typeof(D))]
public class Container
{
}

public class A 
{
    public A(B b, C c){}
}
public class B 
{
    public B(C c, D d){}
}
public class C {}
public class D 
{
    public D(C c){}
}
";
            Compilation comp = CreateCompilation(userSource, MetadataReference.CreateFromFile(typeof(IAsyncContainer<>).Assembly.Location));
            Assert.Empty(comp.GetDiagnostics());
            var registrations = new RegistrationCalculator(comp, x => Assert.False(true, x.ToString()), default).GetRegistrations(comp.AssertGetTypeByMetadataName("Container"));
            var hasErrors = DependencyChecker.HasCircularOrMissingDependencies(comp.AssertGetTypeByMetadataName("A"), isAsync: true, new(registrations), x => Assert.True(false, x.ToString()), ((ClassDeclarationSyntax)comp.AssertGetTypeByMetadataName("Container").DeclaringSyntaxReferences.First().GetSyntax()).Identifier.GetLocation());
            Assert.False(hasErrors);
        }

        [Fact]
        public void IgnoresErrorsInUnusedDependencies()
        {
            string userSource = @"
using StrongInject;

[Registration(typeof(A))]
[Registration(typeof(B))]
[Registration(typeof(C))]
[Registration(typeof(D))]
public class Container
{
}

public class A 
{
    public A(B b, C c, E e){}
}
public class B 
{
    public B(C c, D d){}
}
public class C {}
public class D 
{
    public D(C c){}
}
public class E {}
";
            Compilation comp = CreateCompilation(userSource, MetadataReference.CreateFromFile(typeof(IAsyncContainer<>).Assembly.Location));
            Assert.Empty(comp.GetDiagnostics());
            var registrations = new RegistrationCalculator(comp, x => Assert.False(true, x.ToString()), default).GetRegistrations(comp.AssertGetTypeByMetadataName("Container"));
            var hasErrors = DependencyChecker.HasCircularOrMissingDependencies(comp.AssertGetTypeByMetadataName("B"), isAsync: true, new(registrations), x => Assert.True(false, x.ToString()), ((ClassDeclarationSyntax)comp.AssertGetTypeByMetadataName("Container").DeclaringSyntaxReferences.First().GetSyntax()).Identifier.GetLocation());
            Assert.False(hasErrors);
        }

        [Fact]
        public void ErrorOnCircularDependency1()
        {
            string userSource = @"
using StrongInject;

[Registration(typeof(A))]
[Registration(typeof(B))]
[Registration(typeof(C))]
[Registration(typeof(D))]
public class Container
{
}

public class A 
{
    public A(B b, C c){}
}
public class B 
{
    public B(C c, D d){}
}
public class C 
{
    public C(B b){}
}
public class D 
{
    public D(C c){}
}
";
            Compilation comp = CreateCompilation(userSource, MetadataReference.CreateFromFile(typeof(IAsyncContainer<>).Assembly.Location));
            Assert.Empty(comp.GetDiagnostics());
            var diagnostics = new List<Diagnostic>();
            var registrations = new RegistrationCalculator(comp, x => Assert.False(true, x.ToString()), default).GetRegistrations(comp.AssertGetTypeByMetadataName("Container"));
            var hasErrors = DependencyChecker.HasCircularOrMissingDependencies(comp.AssertGetTypeByMetadataName("A"), isAsync: true, new(registrations), x => diagnostics.Add(x), ((ClassDeclarationSyntax)comp.AssertGetTypeByMetadataName("Container").DeclaringSyntaxReferences.First().GetSyntax()).Identifier.GetLocation());
            Assert.True(hasErrors);
            diagnostics.Verify(
                // (8,14): Error SI0101: Error while resolving dependencies for 'A': 'B' has a circular dependency
                // Container
                new DiagnosticResult("SI0101", @"Container").WithLocation(8, 14));
        }

        [Fact]
        public void ErrorOnCircularDependency2()
        {
            string userSource = @"
using StrongInject;

[Registration(typeof(A))]
[Registration(typeof(B))]
[Registration(typeof(C))]
[Registration(typeof(D))]
public class Container
{
}

public class A 
{
    public A(B b, C c){}
}
public class B 
{
    public B(C c, D d){}
}
public class C 
{
    public C(C c){}
}
public class D 
{
    public D(C c){}
}
";
            Compilation comp = CreateCompilation(userSource, MetadataReference.CreateFromFile(typeof(IAsyncContainer<>).Assembly.Location));
            Assert.Empty(comp.GetDiagnostics());
            var diagnostics = new List<Diagnostic>();
            var registrations = new RegistrationCalculator(comp, x => Assert.False(true, x.ToString()), default).GetRegistrations(comp.AssertGetTypeByMetadataName("Container"));
            var hasErrors = DependencyChecker.HasCircularOrMissingDependencies(comp.AssertGetTypeByMetadataName("A"), isAsync: true, new(registrations), x => diagnostics.Add(x), ((ClassDeclarationSyntax)comp.AssertGetTypeByMetadataName("Container").DeclaringSyntaxReferences.First().GetSyntax()).Identifier.GetLocation());
            Assert.True(hasErrors);
            diagnostics.Verify(
                // (8,14): Error SI0101: Error while resolving dependencies for 'A': 'C' has a circular dependency
                // Container
                new DiagnosticResult("SI0101", @"Container").WithLocation(8, 14));
        }

        [Fact]
        public void ErrorOnCircularDependency3()
        {
            string userSource = @"
using StrongInject;

[Registration(typeof(A))]
[Registration(typeof(B))]
[Registration(typeof(C))]
[Registration(typeof(D))]
public class Container
{
}

public class A 
{
    public A(B b, C c, A a){}
}
public class B 
{
    public B(C c, D d){}
}
public class C {}
public class D 
{
    public D(C c){}
}
";
            Compilation comp = CreateCompilation(userSource, MetadataReference.CreateFromFile(typeof(IAsyncContainer<>).Assembly.Location));
            Assert.Empty(comp.GetDiagnostics());
            var diagnostics = new List<Diagnostic>();
            var registrations = new RegistrationCalculator(comp, x => Assert.False(true, x.ToString()), default).GetRegistrations(comp.AssertGetTypeByMetadataName("Container"));
            var hasErrors = DependencyChecker.HasCircularOrMissingDependencies(comp.AssertGetTypeByMetadataName("A"), isAsync: true, new(registrations), x => diagnostics.Add(x), ((ClassDeclarationSyntax)comp.AssertGetTypeByMetadataName("Container").DeclaringSyntaxReferences.First().GetSyntax()).Identifier.GetLocation());
            Assert.True(hasErrors);
            diagnostics.Verify(
                // (8,14): Error SI0101: Error while resolving dependencies for 'A': 'A' has a circular dependency
                // Container
                new DiagnosticResult("SI0101", @"Container").WithLocation(8, 14));
        }

        [Fact]
        public void ErrorWhenDependencyNotRegistered()
        {
            string userSource = @"
using StrongInject;

[Registration(typeof(A))]
[Registration(typeof(B))]
[Registration(typeof(C))]
public class Container
{
}

public class A 
{
    public A(B b, C c){}
}
public class B 
{
    public B(C c, D d){}
}
public class C {}
public class D 
{
    public D(C c){}
}
";
            Compilation comp = CreateCompilation(userSource, MetadataReference.CreateFromFile(typeof(IAsyncContainer<>).Assembly.Location));
            Assert.Empty(comp.GetDiagnostics());
            var diagnostics = new List<Diagnostic>();
            var registrations = new RegistrationCalculator(comp, x => Assert.False(true, x.ToString()), default).GetRegistrations(comp.AssertGetTypeByMetadataName("Container"));
            var hasErrors = DependencyChecker.HasCircularOrMissingDependencies(comp.AssertGetTypeByMetadataName("A"), isAsync: true, new(registrations), x => diagnostics.Add(x), ((ClassDeclarationSyntax)comp.AssertGetTypeByMetadataName("Container").DeclaringSyntaxReferences.First().GetSyntax()).Identifier.GetLocation());
            Assert.True(hasErrors);
            diagnostics.Verify(
                // (7,14): Error SI0102: Error while resolving dependencies for 'A': We have no source for instance of type 'D'
                // Container
                new DiagnosticResult("SI0102", @"Container").WithLocation(7, 14));
        }

        [Fact]
        public void ErrorForAllMissingDependencies1()
        {
            string userSource = @"
using StrongInject;

[Registration(typeof(A))]
[Registration(typeof(B))]
public class Container
{
}

public class A 
{
    public A(B b, C c){}
}
public class B 
{
    public B(C c, D d){}
}
public class C {}
public class D 
{
}
";
            Compilation comp = CreateCompilation(userSource, MetadataReference.CreateFromFile(typeof(IAsyncContainer<>).Assembly.Location));
            Assert.Empty(comp.GetDiagnostics());
            var diagnostics = new List<Diagnostic>();
            var registrations = new RegistrationCalculator(comp, x => Assert.False(true, x.ToString()), default).GetRegistrations(comp.AssertGetTypeByMetadataName("Container"));
            var hasErrors = DependencyChecker.HasCircularOrMissingDependencies(comp.AssertGetTypeByMetadataName("A"), isAsync: true, new(registrations), x => diagnostics.Add(x), ((ClassDeclarationSyntax)comp.AssertGetTypeByMetadataName("Container").DeclaringSyntaxReferences.First().GetSyntax()).Identifier.GetLocation());
            Assert.True(hasErrors);
            diagnostics.Verify(
                // (6,14): Error SI0102: Error while resolving dependencies for 'A': We have no source for instance of type 'C'
                // Container
                new DiagnosticResult("SI0102", @"Container", DiagnosticSeverity.Error).WithLocation(6, 14),
                // (6,14): Error SI0102: Error while resolving dependencies for 'A': We have no source for instance of type 'D'
                // Container
                new DiagnosticResult("SI0102", @"Container", DiagnosticSeverity.Error).WithLocation(6, 14));
        }

        [Fact]
        public void ErrorForAllMissingDependencies2()
        {
            string userSource = @"
using StrongInject;

[Registration(typeof(A))]
[Registration(typeof(B))]
public class Container
{
}

public class A 
{
    public A(B b, C c){}
}
public class B 
{
    public B(D d, E e){}
}
public class C {}
public class D {}
public class E {}
";
            Compilation comp = CreateCompilation(userSource, MetadataReference.CreateFromFile(typeof(IAsyncContainer<>).Assembly.Location));
            Assert.Empty(comp.GetDiagnostics());
            var diagnostics = new List<Diagnostic>();
            var registrations = new RegistrationCalculator(comp, x => Assert.False(true, x.ToString()), default).GetRegistrations(comp.AssertGetTypeByMetadataName("Container"));
            var hasErrors = DependencyChecker.HasCircularOrMissingDependencies(comp.AssertGetTypeByMetadataName("A"), isAsync: true, new(registrations), x => diagnostics.Add(x), ((ClassDeclarationSyntax)comp.AssertGetTypeByMetadataName("Container").DeclaringSyntaxReferences.First().GetSyntax()).Identifier.GetLocation());
            Assert.True(hasErrors);
            diagnostics.Verify(
                // (6,14): Error SI0102: Error while resolving dependencies for 'A': We have no source for instance of type 'D'
                // Container
                new DiagnosticResult("SI0102", @"Container", DiagnosticSeverity.Error).WithLocation(6, 14),
                // (6,14): Error SI0102: Error while resolving dependencies for 'A': We have no source for instance of type 'E'
                // Container
                new DiagnosticResult("SI0102", @"Container", DiagnosticSeverity.Error).WithLocation(6, 14),
                // (6,14): Error SI0102: Error while resolving dependencies for 'A': We have no source for instance of type 'C'
                // Container
                new DiagnosticResult("SI0102", @"Container", DiagnosticSeverity.Error).WithLocation(6, 14));
        }
    }
}
