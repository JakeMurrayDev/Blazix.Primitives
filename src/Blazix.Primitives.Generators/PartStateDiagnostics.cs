using Microsoft.CodeAnalysis;

namespace Blazix.Primitives.Generators;

/// <summary>Provides the consumer-facing diagnostics that the <see cref="PartStateGenerator"/> reports.</summary>
/// <remarks>Every diagnostic id has a test in <c>Tests.Generators</c>.</remarks>
internal static class PartStateDiagnostics
{
    private const string Category = "Blazix.Primitives";

    /// <summary>The BLZX1001 diagnostic, reported when a <c>[PartState]</c> type is not a record class.</summary>
    public static readonly DiagnosticDescriptor NotARecordClass = new(
        "BLZX1001",
        "A [PartState] type must be a sealed partial record class",
        "'{0}' is marked [PartState] but is not a record class; declare it as a sealed partial record",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    // ADR 08 §3
    /// <summary>The BLZX1002 diagnostic, reported when a <c>[PartState]</c> record is not sealed.</summary>
    public static readonly DiagnosticDescriptor NotSealed = new(
        "BLZX1002",
        "A [PartState] record must be sealed",
        "'{0}' is marked [PartState] but is not sealed; a state record is a leaf type that nothing derives from",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>The BLZX1003 diagnostic, reported when a property of a <c>[PartState]</c> record has a type that cannot be projected.</summary>
    public static readonly DiagnosticDescriptor NotPrimitiveProperty = new(
        "BLZX1003",
        "A [PartState] property must be projectable",
        "Property '{0}' of [PartState] record '{1}' has type '{2}', which cannot be projected to a data-* attribute; use a bool, number, string or enum (or their nullable forms), project it through a custom mapping on a primitive field, or mark it [MirroredField] to keep it out of the projection",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>The BLZX1004 diagnostic, reported when a <c>[PartState]</c> record or one of its containing types is not partial.</summary>
    public static readonly DiagnosticDescriptor NotPartial = new(
        "BLZX1004",
        "A [PartState] record and its containing types must be partial",
        "'{0}' is marked [PartState] but it or one of its containing types is not partial, so the projection cannot be generated into it",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
