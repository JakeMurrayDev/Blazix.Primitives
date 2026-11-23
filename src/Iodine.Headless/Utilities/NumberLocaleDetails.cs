namespace Iodine.Headless.Utilities;

// NUM-I18N-07: Base UI's getNumberLocaleDetails, the last value of each part type of formatToParts(11111.1) under the culture
// and options, the decimal always the plain culture's.
/// <summary>Represents the symbols a culture and a set of number format options render.</summary>
internal sealed class NumberLocaleDetails
{
    // Base UI's SAMPLE_FORMAT_NUMBER: a grouping separator and a fraction, so every locale-specific part is emitted.
    /// <summary>The number whose parts surface every symbol of a format.</summary>
    public const double SampleNumber = 11111.1;

    /// <summary>Gets or sets the last integer digits.</summary>
    public string? Integer { get; set; }

    /// <summary>Gets or sets the group separator.</summary>
    public string? Group { get; set; }

    /// <summary>Gets or sets the decimal separator, which is never <see langword="null"/> once the details are built.</summary>
    public string? Decimal { get; set; }

    /// <summary>Gets or sets the fraction digits.</summary>
    public string? Fraction { get; set; }

    /// <summary>Gets or sets the minus sign.</summary>
    public string? MinusSign { get; set; }

    /// <summary>Gets or sets the plus sign.</summary>
    public string? PlusSign { get; set; }

    /// <summary>Gets or sets the percent sign.</summary>
    public string? PercentSign { get; set; }

    /// <summary>Gets or sets the currency text.</summary>
    public string? Currency { get; set; }

    /// <summary>Gets or sets the unit text.</summary>
    public string? Unit { get; set; }

    /// <summary>Gets or sets the last literal.</summary>
    public string? Literal { get; set; }

    /// <summary>Gets or sets the exponent separator.</summary>
    public string? ExponentSeparator { get; set; }

    /// <summary>Gets or sets the exponent's minus sign.</summary>
    public string? ExponentMinusSign { get; set; }

    /// <summary>Gets or sets the exponent digits.</summary>
    public string? ExponentInteger { get; set; }

    /// <summary>Gets or sets the compact suffix.</summary>
    public string? Compact { get; set; }

    /// <summary>Sets the value of a part type, the last one winning.</summary>
    /// <param name="type">The part type.</param>
    /// <param name="value">The part's text.</param>
    public void Set(NumberFormatPartType type, string value)
    {
        switch (type)
        {
            case NumberFormatPartType.Integer: Integer = value; break;
            case NumberFormatPartType.Group: Group = value; break;
            case NumberFormatPartType.Decimal: Decimal = value; break;
            case NumberFormatPartType.Fraction: Fraction = value; break;
            case NumberFormatPartType.MinusSign: MinusSign = value; break;
            case NumberFormatPartType.PlusSign: PlusSign = value; break;
            case NumberFormatPartType.PercentSign: PercentSign = value; break;
            case NumberFormatPartType.Currency: Currency = value; break;
            case NumberFormatPartType.Unit: Unit = value; break;
            case NumberFormatPartType.Literal: Literal = value; break;
            case NumberFormatPartType.ExponentSeparator: ExponentSeparator = value; break;
            case NumberFormatPartType.ExponentMinusSign: ExponentMinusSign = value; break;
            case NumberFormatPartType.ExponentInteger: ExponentInteger = value; break;
            case NumberFormatPartType.Compact: Compact = value; break;
            default: break;
        }
    }
}
