using Microsoft.CodeAnalysis;

namespace Iodine.Headless.Generators;

/// <summary>Provides the consumer-facing diagnostics that the <see cref="StoreStateGenerator"/> reports.</summary>
/// <remarks>Every diagnostic id has a test in <c>Tests.Generators</c>.</remarks>
internal static class StoreStateDiagnostics
{
    private const string Category = "Iodine.Headless";

    /// <summary>The IOD1005 diagnostic, reported when a <c>[StoreState]</c> type is not a record class.</summary>
    public static readonly DiagnosticDescriptor NotARecordClass = new(
        "IOD1005",
        "A [StoreState] type must be a sealed partial record class",
        "'{0}' is marked [StoreState] but is not a record class; declare it as a sealed partial record",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>The IOD1006 diagnostic, reported when a <c>[StoreState]</c> record is not sealed.</summary>
    public static readonly DiagnosticDescriptor NotSealed = new(
        "IOD1006",
        "A [StoreState] record must be sealed",
        "'{0}' is marked [StoreState] but is not sealed; a derived record would add members its generated comparison never sees",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>The IOD1007 diagnostic, reported when a <c>[StoreState]</c> record or one of its containing types is not partial.</summary>
    public static readonly DiagnosticDescriptor NotPartial = new(
        "IOD1007",
        "A [StoreState] record and its containing types must be partial",
        "'{0}' is marked [StoreState] but it or one of its containing types is not partial, so Copy and ShallowEquals cannot be generated into it",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>The IOD1008 diagnostic, reported when a <c>[StoreState]</c> record does not implement <c>IStoreState</c> of itself.</summary>
    public static readonly DiagnosticDescriptor NotStoreState = new(
        "IOD1008",
        "A [StoreState] record must implement IStoreState of itself",
        "'{0}' is marked [StoreState] but does not implement IStoreState<{0}>, so no store can hold it",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>The IOD1009 diagnostic, reported when a <c>[StoreState]</c> record declares <c>Copy</c> or <c>ShallowEquals</c> itself.</summary>
    public static readonly DiagnosticDescriptor DeclaresGeneratedMember = new(
        "IOD1009",
        "A [StoreState] record must not declare Copy or ShallowEquals",
        "'{0}' is marked [StoreState] and declares {1} itself; remove it, the generator writes {1} from the record's members",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>The IOD1010 diagnostic, reported when a <c>[StoreState]</c> record derives from another record.</summary>
    public static readonly DiagnosticDescriptor DerivesFromRecord = new(
        "IOD1010",
        "A [StoreState] record must not derive from another record",
        "'{0}' is marked [StoreState] but derives from '{1}'; the generated ShallowEquals compares only the record's own members, so declare them all on '{0}'",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// The IOD1011 diagnostic, reported when a member of a <c>[StoreState]</c> record has a struct type with neither an
    /// <c>==</c> operator nor <c>IEquatable&lt;T&gt;</c>, so that its comparison boxes it.
    /// </summary>
    public static readonly DiagnosticDescriptor BoxedMember = new(
        "IOD1011",
        "A [StoreState] member of a struct type should be comparable without boxing",
        "Member '{0}' of [StoreState] record '{1}' has type '{2}', a struct with neither an == operator nor IEquatable<{2}>, so it is compared through Equality.Is, which boxes it; implement IEquatable<{2}> on the struct, or hold the value in a type that has one",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}
