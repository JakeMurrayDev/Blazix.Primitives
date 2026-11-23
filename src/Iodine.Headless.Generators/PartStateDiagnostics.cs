using Microsoft.CodeAnalysis;

namespace Iodine.Headless.Generators;

// Every diagnostic id has a test in Tests.Generators.
/// <summary>Provides the consumer-facing diagnostics that the <see cref="PartStateGenerator"/> reports.</summary>
internal static class PartStateDiagnostics
{
    private const string Category = "Iodine.Headless";

    /// <summary>Represents the IOD1001 diagnostic, which is reported when a type marked <c>[PartState]</c> is not a record class.</summary>
    public static readonly DiagnosticDescriptor NotARecordClass = new(
        "IOD1001",
        "A [PartState] type must be a sealed partial record class",
        "Iodine: `{0}` is marked `[PartState]` but is not a record class; declare it as a `sealed partial record`",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    // ADR 08 §3
    /// <summary>Represents the IOD1002 diagnostic, which is reported when a record marked <c>[PartState]</c> is not sealed.</summary>
    public static readonly DiagnosticDescriptor NotSealed = new(
        "IOD1002",
        "A [PartState] record must be sealed",
        "Iodine: `{0}` is marked `[PartState]` but is not sealed; a state record is a leaf type that nothing derives from",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>Represents the IOD1003 diagnostic, which is reported when a property of a record marked <c>[PartState]</c> has a type that cannot be projected.</summary>
    public static readonly DiagnosticDescriptor NotPrimitiveProperty = new(
        "IOD1003",
        "A [PartState] property must be projectable",
        "Iodine: property `{0}` of `[PartState]` record `{1}` has type `{2}`, which cannot be projected to a `data-*` attribute; use a `bool`, number, `string` or enum (or their nullable forms), project it through a custom mapping on a primitive field, mark it `[NotProjected]` to keep it in the state without an attribute, or mark it `[MirroredField]` when JavaScript writes its attribute",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>Represents the IOD1004 diagnostic, which is reported when a record marked <c>[PartState]</c>, or one of its containing types, is not partial.</summary>
    public static readonly DiagnosticDescriptor NotPartial = new(
        "IOD1004",
        "A [PartState] record and its containing types must be partial",
        "Iodine: `{0}` is marked `[PartState]` but it or one of its containing types is not partial, so the projection cannot be generated into it",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
