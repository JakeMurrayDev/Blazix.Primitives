namespace Blazix.Primitives;

// NUM-API-12, NUM-I18N-01; ADR 08 Amendments 2026-11-18 (decision 10 (a), chosen by the user 2026-11-18): the options record
// of NumberFieldRoot.Format, limited to what NUM's rows and the pinned tests use. The members mirror Intl.NumberFormatOptions
// one for one; a null member is an option not supplied, which matters because the presence of a rounding option changes the
// step pipeline (NUM-STA-10). The formatter that reads it is Utilities/NumberFormatter.cs.
/// <summary>Represents the options that format a number field's value, such as its style, its digits and its rounding.</summary>
/// <remarks>
/// <para>
/// The options follow the number formatting of web browsers. Formatting uses the .NET culture data of the field's culture, so the
/// text is equivalent to a browser's, not identical to it in every culture.
/// </para>
/// <para>Two instances with equal members are equal, so passing an equal record again does not re-format the field.</para>
/// </remarks>
public sealed record NumberFormatOptions
{
    /// <summary>Gets the formatting style.</summary>
    /// <value>The style. The default is <see cref="NumberFormatStyle.Decimal"/>.</value>
    public NumberFormatStyle Style { get; init; }

    /// <summary>Gets the ISO 4217 code of the currency, such as <c>"USD"</c> or <c>"EUR"</c>.</summary>
    /// <value>The currency code, required when <see cref="Style"/> is <see cref="NumberFormatStyle.Currency"/>. The default is <see langword="null"/>.</value>
    public string? Currency { get; init; }

    /// <summary>Gets how the currency is displayed.</summary>
    /// <value>The currency display. The default is <see cref="NumberFormatCurrencyDisplay.Symbol"/>.</value>
    public NumberFormatCurrencyDisplay CurrencyDisplay { get; init; }

    /// <summary>Gets how a negative currency amount is signed.</summary>
    /// <value>The currency sign. The default is <see cref="NumberFormatCurrencySign.Standard"/>.</value>
    public NumberFormatCurrencySign CurrencySign { get; init; }

    /// <summary>Gets the unit, required when <see cref="Style"/> is <see cref="NumberFormatStyle.Unit"/>.</summary>
    /// <value>The unit, or <see langword="null"/> when none is supplied. The default is <see langword="null"/>.</value>
    public NumberFormatUnit? Unit { get; init; }

    /// <summary>Gets the notation.</summary>
    /// <value>The notation. The default is <see cref="NumberFormatNotation.Standard"/>.</value>
    public NumberFormatNotation Notation { get; init; }

    /// <summary>Gets when the sign is displayed.</summary>
    /// <value>The sign display. The default is <see cref="NumberFormatSignDisplay.Auto"/>.</value>
    public NumberFormatSignDisplay SignDisplay { get; init; }

    /// <summary>Gets a value indicating whether the integer digits are grouped, as in <c>1,234</c>.</summary>
    /// <value><see langword="true"/> to group the integer digits; otherwise, <see langword="false"/>. The default is <see langword="true"/>.</value>
    public bool UseGrouping { get; init; } = true;

    /// <summary>Gets the minimum number of integer digits, from 1 to 21; shorter integers are padded with leading zeros.</summary>
    /// <value>The minimum number of integer digits, or <see langword="null"/> for 1. The default is <see langword="null"/>.</value>
    public int? MinimumIntegerDigits { get; init; }

    /// <summary>Gets the minimum number of fraction digits, from 0 to 100.</summary>
    /// <value>
    /// The minimum number of fraction digits, or <see langword="null"/> for the style's default: the currency's digits for
    /// <see cref="NumberFormatStyle.Currency"/>, otherwise 0. The default is <see langword="null"/>.
    /// </value>
    public int? MinimumFractionDigits { get; init; }

    /// <summary>Gets the maximum number of fraction digits, from 0 to 100.</summary>
    /// <value>
    /// The maximum number of fraction digits, or <see langword="null"/> for the style's default: the currency's digits for
    /// <see cref="NumberFormatStyle.Currency"/>, 0 for <see cref="NumberFormatStyle.Percent"/>, otherwise 3, and never less than
    /// <see cref="MinimumFractionDigits"/>. The default is <see langword="null"/>.
    /// </value>
    public int? MaximumFractionDigits { get; init; }

    /// <summary>Gets the minimum number of significant digits, from 1 to 21.</summary>
    /// <value>The minimum number of significant digits, or <see langword="null"/> when none is supplied. The default is <see langword="null"/>.</value>
    public int? MinimumSignificantDigits { get; init; }

    /// <summary>Gets the maximum number of significant digits, from 1 to 21.</summary>
    /// <value>The maximum number of significant digits, or <see langword="null"/> when none is supplied. The default is <see langword="null"/>.</value>
    public int? MaximumSignificantDigits { get; init; }

    /// <summary>Gets how a value is rounded to the displayed digits.</summary>
    /// <value>The rounding mode, or <see langword="null"/> for <see cref="NumberFormatRoundingMode.HalfExpand"/>. The default is <see langword="null"/>.</value>
    public NumberFormatRoundingMode? RoundingMode { get; init; }

    /// <summary>
    /// Gets the increment, in units of the last fraction digit, that a value is rounded to: one of 1, 2, 5, 10, 20, 25, 50, 100,
    /// 200, 250, 500, 1000, 2000, 2500 and 5000.
    /// </summary>
    /// <value>
    /// The rounding increment, or <see langword="null"/> for 1. An increment other than 1 requires equal
    /// <see cref="MinimumFractionDigits"/> and <see cref="MaximumFractionDigits"/> and no significant digits. The default is
    /// <see langword="null"/>.
    /// </value>
    public int? RoundingIncrement { get; init; }

    /// <summary>Gets how the fraction digits and the significant digits are reconciled when both are supplied.</summary>
    /// <value>The rounding priority, or <see langword="null"/> for <see cref="NumberFormatRoundingPriority.Auto"/>. The default is <see langword="null"/>.</value>
    public NumberFormatRoundingPriority? RoundingPriority { get; init; }
}
