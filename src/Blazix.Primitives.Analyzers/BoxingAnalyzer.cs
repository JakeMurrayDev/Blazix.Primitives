using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Blazix.Primitives.Analyzers;

// ADR 05 §4 (no boxing where the port controls it; a union never travels through an `object` slot, Amendments
// 2026-10-20); ADR 16 §C. The allowed sites are Blazor's own `object` contracts, listed in CONTEXT.md.
/// <summary>
/// Rejects boxing in library code: a union converted to <c>object</c> or an interface anywhere, any other value type
/// converted to <c>object</c> or an interface outside Blazor's own <c>object</c> contracts, and the members of
/// <c>System.Enum</c> that box their receiver or argument.
/// </summary>
/// <remarks>
/// The allowed sites are the <c>object</c> value of <c>RenderTreeBuilder.AddAttribute</c> and
/// <c>RenderTreeBuilder.AddComponentParameter</c>, the argument of <c>RenderTreeBuilder.AddMultipleAttributes</c>, the
/// argument arrays of the <c>Invoke*</c> methods of <c>IJSRuntime</c> and <c>IJSObjectReference</c> (with the in-process
/// interfaces and the extension methods on them), and a value stored into a dictionary of <c>string</c> to
/// <c>object</c>, the type of the unmatched attribute bag.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class BoxingAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Represents the id of the diagnostics this analyzer reports, <c>BLZX2005</c>.</summary>
    public const string DiagnosticId = "BLZX2005";

    // ADR 05 §4, Amendments 2026-10-20 (union-binding spike)
    private static readonly DiagnosticDescriptor s_unionRule = new(
        DiagnosticId,
        "A union never converts to `object` or an interface",
        "Blazix: `{0}` converts the union `{1}` to `{2}`, which boxes it and hides its case; pass the case value instead",
        "Blazix.Primitives",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Boxing a union struct hides which case it holds, so a union never travels through an `object` slot, not even one of Blazor's: the unmatched attribute bag and the attribute methods of `RenderTreeBuilder` receive the case value.");

    // ADR 05 §4; ADR 16 §C
    private static readonly DiagnosticDescriptor s_valueRule = new(
        DiagnosticId,
        "Library code does not box value types",
        "Blazix: `{0}` boxes the `{1}` value into `{2}`; use a typed overload or a generic parameter instead",
        "Blazix.Primitives",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A value type converted to `object` or to an interface allocates on every call. Library code passes value types through typed overloads and generics; the only conversions allowed are into Blazor's own `object` contracts: the value of `RenderTreeBuilder.AddAttribute` and `AddComponentParameter`, the argument of `AddMultipleAttributes`, the argument arrays of the JavaScript interop `Invoke*` methods, and a value stored into a dictionary of `string` to `object`.");

    // ADR 05 §4; ADR 16 §C
    private static readonly DiagnosticDescriptor s_enumRule = new(
        DiagnosticId,
        "Library code does not call the boxing members of `Enum`",
        "Blazix: `{0}` boxes the enum `{1}`; {2}",
        "Blazix.Primitives",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "`Enum.ToString`, `Enum.HasFlag`, `Enum.Equals(object)`, `Enum.GetValues` and `Enum.GetNames` box their enum or work by reflection. Library code switches over the members, tests flags with `&`, compares with `==` and lists the members it needs.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(s_unionRule, s_valueRule, s_enumRule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        // The tests-first stub: it registers nothing, so every violation test is red until the implementation lands.
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
    }
}
