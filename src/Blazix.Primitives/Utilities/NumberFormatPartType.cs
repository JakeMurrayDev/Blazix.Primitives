namespace Blazix.Primitives.Utilities;

// UTL-I18N-05, NUM-I18N-07: the part types of Intl.NumberFormat.prototype.formatToParts that the port's formatter emits.
/// <summary>Specifies the kind of a piece of formatted number text.</summary>
internal enum NumberFormatPartType
{
    /// <summary>A run of integer digits between group separators.</summary>
    Integer,

    /// <summary>A group separator.</summary>
    Group,

    /// <summary>The decimal separator.</summary>
    Decimal,

    /// <summary>The fraction digits.</summary>
    Fraction,

    /// <summary>The minus sign.</summary>
    MinusSign,

    /// <summary>The plus sign.</summary>
    PlusSign,

    /// <summary>The percent sign.</summary>
    PercentSign,

    /// <summary>The currency symbol, code or name.</summary>
    Currency,

    /// <summary>The unit text.</summary>
    Unit,

    /// <summary>Literal text, such as a space or a parenthesis.</summary>
    Literal,

    /// <summary>The exponent separator of scientific notation.</summary>
    ExponentSeparator,

    /// <summary>The minus sign of a negative exponent.</summary>
    ExponentMinusSign,

    /// <summary>The exponent digits.</summary>
    ExponentInteger,

    /// <summary>The compact notation suffix, such as <c>K</c>.</summary>
    Compact,

    /// <summary>The text of a value that is not a number.</summary>
    Nan,

    /// <summary>The infinity symbol.</summary>
    Infinity,
}
