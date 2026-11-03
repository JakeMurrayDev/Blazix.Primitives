namespace Blazix.Primitives.Utilities;

// ADR 08 Amendments 2026-10-22 (any-typed values): `null` means nothing selected, and a radio whose value is `null` behaves
// as Base UI's `undefined` radio (RAD-EVT-03, RAD-DOM-11). Moved from Radio/RadioValues.cs beside the item equality by M5
// track 4 (design point 1, settled by the orchestrator 2026-11-04); its users are Radio, RadioGroup, Tabs, the item
// equality (UTL-ALG-06) and the form value. The render path tests every value: `value is null` boxes a value type wherever
// the code is not optimized (tier-0 code on CoreCLR, the WebAssembly interpreter), so a value type is tested without the
// box. A reference type's box is the reference itself.
/// <summary>Provides the null test of a value-typed part's value.</summary>
internal static class ValueNull
{
    /// <summary>Determines whether a value is <see langword="null"/>.</summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> for a <see langword="null"/> reference or an empty nullable value.</returns>
    public static bool IsNull<TValue>(TValue value)
    {
        if (!typeof(TValue).IsValueType)
        {
            return value is null;
        }

        return NullableValue<TValue>.IsNullable && EqualityComparer<TValue>.Default.Equals(value, default!);
    }

    // Whether a value type is a Nullable<T>, read once per type.
    private static class NullableValue<TValue>
    {
        public static readonly bool IsNullable = Nullable.GetUnderlyingType(typeof(TValue)) is not null;
    }
}
