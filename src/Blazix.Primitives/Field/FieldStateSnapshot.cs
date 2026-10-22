namespace Blazix.Primitives;

// FFI-STA-01, FLD-STA-01..05, FLD §3.1 (the root state object every part spreads into its own). The issue's "field-state
// snapshot record every control embeds": tracks 4-8 build their state records from it with their own Disabled.
/// <summary>The field's state as every part and control reads it.</summary>
/// <param name="Disabled">Whether the field is disabled (its own parameter or an enclosing fieldset).</param>
/// <param name="Touched">Whether the control has reached its touched boundary, or the field's touched parameter.</param>
/// <param name="Dirty">Whether the value differs from the initial value, or the field's dirty parameter.</param>
/// <param name="Valid">The combined verdict: <see langword="true"/>, <see langword="false"/>, or <see langword="null"/> while unknown or disabled.</param>
/// <param name="Filled">Whether the control has a value.</param>
/// <param name="Focused">Whether the control owns focus.</param>
internal readonly record struct FieldStateSnapshot(bool Disabled, bool Touched, bool Dirty, bool? Valid, bool Filled, bool Focused)
{
    /// <summary>Gets the state outside a field: everything clear and the verdict unknown.</summary>
    /// <value>The default state.</value>
    public static FieldStateSnapshot Default => default;
}
