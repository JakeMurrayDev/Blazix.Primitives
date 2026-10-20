using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Blazix.Primitives.Analyzers;

// ADR 08 §5 (the eleven groups); the placements of constructors, events, operators, explicit interface implementations,
// protected members and nested types are this analyzer's, listed in CONTEXT.md
/// <summary>
/// Requires the members of every type to appear in the library's member order.
/// </summary>
/// <remarks>
/// The order is constants, fields, constructors, private properties, parameter properties, public properties, internal
/// properties, lifecycle methods, dispose methods, public methods, internal methods, private methods, nested types. A member
/// is out of order when a member of a later group comes before it in the same declaration.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MemberOrderAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Represents the id of the diagnostic this analyzer reports, <c>BLZX2003</c>.</summary>
    public const string DiagnosticId = "BLZX2003";

    // ADR 08 §5
    private static readonly DiagnosticDescriptor s_rule = new(
        DiagnosticId,
        "Members follow the library's member order",
        "Blazix: `{0}` is one of the {1} and must come before `{2}`, one of the {3}",
        "Blazix.Primitives",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Members appear in this order: constants; fields (field-like events included); constructors (static constructor and finalizer included); private properties; parameter properties ([Parameter] and cascading parameters, whatever their accessibility); public properties; internal properties; lifecycle methods (SetParametersAsync, OnInitialized*, OnParametersSet*, OnAfterRender*, ShouldRender, BuildRenderTree); dispose methods (Dispose, DisposeAsync, DisposeAsyncCore); public methods (operators included); internal methods; private methods; nested types. Protected and protected internal members order as public, private protected members as internal, and an explicit interface implementation as public unless its name makes it a lifecycle or dispose method.");

    private static readonly ImmutableArray<string> s_groupNames = ImmutableArray.Create(
        "constants",
        "fields",
        "constructors",
        "private properties",
        "parameter properties",
        "public properties",
        "internal properties",
        "lifecycle methods",
        "dispose methods",
        "public methods",
        "internal methods",
        "private methods",
        "nested types");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(s_rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        // The tests-first stub: it registers nothing, so every violation test is red until the implementation lands.
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
    }
}
