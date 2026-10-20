using Blazix.Primitives.Analyzers;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Blazix.Primitives.Tests.Generators.Analyzers;

/// <summary>
/// The fixed-cascade analyzer (ADR 16 rule 9): a <c>CascadingValue&lt;T&gt;</c> opened by library render code without
/// <c>IsFixed</c> set to <c>true</c> is a build error. The compilation references <c>Microsoft.AspNetCore.Components</c>
/// 10.0 on top of the .NET 10 reference assemblies, the newest the testing SDK resolves.
/// </summary>
public sealed class FixedCascadeAnalyzerTests
{
    private static readonly ReferenceAssemblies s_withComponents = ReferenceAssemblies.Net.Net100.AddPackages(
        [new PackageIdentity("Microsoft.AspNetCore.Components", "10.0.0")]);

    [Fact]
    [Trait("Requirement", "ADR16-9")]
    public async Task A_cascade_with_IsFixed_true_is_accepted()
    {
        const string source = """
            using Microsoft.AspNetCore.Components;
            using Microsoft.AspNetCore.Components.Rendering;

            internal sealed class Store { }

            internal sealed class Root : ComponentBase
            {
                private readonly Store store = new();

                protected override void BuildRenderTree(RenderTreeBuilder builder)
                {
                    builder.OpenComponent<CascadingValue<Store>>(0);
                    builder.AddComponentParameter(1, nameof(CascadingValue<Store>.Value), store);
                    builder.AddComponentParameter(2, nameof(CascadingValue<Store>.IsFixed), true);
                    builder.AddComponentParameter(3, "ChildContent", (RenderFragment)(inner =>
                    {
                        inner.OpenElement(0, "div");
                        inner.CloseElement();
                    }));
                    builder.CloseComponent();

                    builder.OpenComponent(4, typeof(CascadingValue<int>));
                    builder.AddAttribute(5, "Value", 1);
                    builder.AddAttribute(6, "IsFixed", true);
                    builder.CloseComponent();
                }
            }
            """;

        await Verify(source);
    }

    [Fact]
    [Trait("Requirement", "ADR16-9")]
    public async Task A_cascade_without_IsFixed_is_an_error()
    {
        const string source = """
            using Microsoft.AspNetCore.Components;
            using Microsoft.AspNetCore.Components.Rendering;

            internal sealed class Store { }

            internal sealed class Root : ComponentBase
            {
                private readonly Store store = new();

                protected override void BuildRenderTree(RenderTreeBuilder builder)
                {
                    {|#0:builder.OpenComponent<CascadingValue<Store>>(0)|};
                    builder.AddComponentParameter(1, "Value", store);
                    builder.OpenComponent<CascadingValue<int>>(2);
                    builder.AddComponentParameter(3, "IsFixed", true);
                    builder.CloseComponent();
                    builder.CloseComponent();

                    {|#1:builder.OpenComponent(4, typeof(CascadingValue<string>))|};
                    builder.AddAttribute(5, "Value", "x");
                    builder.CloseComponent();
                    builder.OpenElement(6, "div");
                    builder.AddAttribute(7, "IsFixed", true);
                    builder.CloseElement();
                }
            }
            """;

        await Verify(
            source,
            new DiagnosticResult(FixedCascadeAnalyzer.DiagnosticId, Microsoft.CodeAnalysis.DiagnosticSeverity.Error).WithLocation(0).WithArguments("Store"),
            new DiagnosticResult(FixedCascadeAnalyzer.DiagnosticId, Microsoft.CodeAnalysis.DiagnosticSeverity.Error).WithLocation(1).WithArguments("string"));
    }

    [Fact]
    [Trait("Requirement", "ADR16-9")]
    public async Task A_cascade_with_IsFixed_false_is_an_error()
    {
        const string source = """
            using Microsoft.AspNetCore.Components;
            using Microsoft.AspNetCore.Components.Rendering;

            internal sealed class Store { }

            internal sealed class Root : ComponentBase
            {
                private readonly Store store = new();
                private bool isFixed = true;

                protected override void BuildRenderTree(RenderTreeBuilder builder)
                {
                    {|#0:builder.OpenComponent<CascadingValue<Store>>(0)|};
                    builder.AddComponentParameter(1, "Value", store);
                    builder.AddComponentParameter(2, "IsFixed", false);
                    builder.CloseComponent();

                    {|#1:builder.OpenComponent<CascadingValue<Store>>(3)|};
                    builder.AddComponentParameter(4, "Value", store);
                    builder.AddComponentParameter(5, "IsFixed", isFixed);
                    builder.CloseComponent();
                }
            }
            """;

        await Verify(
            source,
            new DiagnosticResult(FixedCascadeAnalyzer.DiagnosticId, Microsoft.CodeAnalysis.DiagnosticSeverity.Error).WithLocation(0).WithArguments("Store"),
            new DiagnosticResult(FixedCascadeAnalyzer.DiagnosticId, Microsoft.CodeAnalysis.DiagnosticSeverity.Error).WithLocation(1).WithArguments("Store"));
    }

    private static async Task Verify(string source, params DiagnosticResult[] expected)
    {
        CSharpAnalyzerTest<FixedCascadeAnalyzer, DefaultVerifier> test = new()
        {
            TestCode = source,
            ReferenceAssemblies = s_withComponents,
        };
        test.ExpectedDiagnostics.AddRange(expected);

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
