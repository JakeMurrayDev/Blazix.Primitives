using System.Globalization;

namespace Blazix.Primitives;

// ADR 08 Amendments 2026-10-22 (any-typed values): `null` means nothing selected, and a radio whose value is `null` behaves
// as Base UI's `undefined` radio (RAD-EVT-03, RAD-DOM-11). FFI-ALG-11, RAD-FRM-02 (internals/serializeValue.ts): Radio is
// M2's only caller with a value that is not a string, so the serializer lives here until Select (M5) moves it to Field/.
// A string is submitted as is; any other value as its text under the invariant culture, built by an interpolated
// string.Create, which takes the value unboxed (radio.md's dated deviation note: `True`, enum names and record text
// differ from JSON.stringify, which would need reflection, ADR 16 rule 1).
/// <summary>Provides the null test and the form value of a radio's value.</summary>
internal static class RadioValues
{
    // The render path tests every radio's value: `value is null` boxes a value type wherever the code is not optimized
    // (tier-0 code on CoreCLR, the WebAssembly interpreter), so a value type is tested without the box. A reference type's
    // box is the reference itself.
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

    /// <summary>Gets the text a hidden radio input submits for a value.</summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>
    /// The string itself, the value's invariant text for any other type, or <see langword="null"/> for a
    /// <see langword="null"/> value, which renders no <c>value</c> attribute.
    /// </returns>
    public static string? Serialize<TValue>(TValue value)
    {
        if (IsNull(value))
        {
            return null;
        }

        if (typeof(TValue) == typeof(string))
        {
            return (string)(object)value!;
        }

        return string.Create(CultureInfo.InvariantCulture, $"{value}");
    }

    // Whether a value type is a Nullable<T>, read once per type.
    private static class NullableValue<TValue>
    {
        public static readonly bool IsNullable = Nullable.GetUnderlyingType(typeof(TValue)) is not null;
    }
}
