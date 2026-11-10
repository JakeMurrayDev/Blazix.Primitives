using System.Globalization;
using Blazix.Primitives.Utilities;

namespace Blazix.Primitives;

// FFI-ALG-11, RAD-FRM-02 (internals/serializeValue.ts): the text a hidden form input submits for a value-typed part (Radio,
// and from M5 Select, Combobox and Autocomplete). Moved from Radio/RadioValues.cs by M5 track 4 (chosen by the user
// 2026-11-03, plan decision 10 (a); design point 1 settled by the orchestrator 2026-11-04). A string is submitted as is; any
// other value as its text under the invariant culture, built by an interpolated string.Create, which takes the value unboxed
// (radio.md's dated deviation note: `True`, enum names and record text differ from JSON.stringify, which would need
// reflection, ADR 16 rule 1). FFI-EDGE-05 holds by construction: there is no JSON, so nothing throws.
/// <summary>Provides the text a hidden form input submits for a value.</summary>
internal static class FormValue
{
    /// <summary>Gets the text a hidden form input submits for a value.</summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>
    /// The string itself, the value's invariant text for any other type, or <see langword="null"/> for a
    /// <see langword="null"/> value, which renders no <c>value</c> attribute.
    /// </returns>
    public static string? Serialize<TValue>(TValue value)
    {
        if (ValueNull.IsNull(value))
        {
            return null;
        }

        if (typeof(TValue) == typeof(string))
        {
            return (string)(object)value!;
        }

        // JavaScript's String(): a boolean is "true" or "false", where C# formats "True" or "False" (CMB-API-68; settled by
        // the orchestrator 2026-11-10).
        if (typeof(TValue) == typeof(bool) || typeof(TValue) == typeof(bool?))
        {
            return (bool)(object)value! ? "true" : "false";
        }

        return string.Create(CultureInfo.InvariantCulture, $"{value}");
    }
}
