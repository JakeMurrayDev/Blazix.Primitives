using System.Collections.Concurrent;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace Iodine.Headless.Utilities;

// UTL-I18N-05..07, NUM-I18N-01, NUM-I18N-07, NUM-I18N-13; ADR 01 decision 2 as amended (equivalent to Intl.NumberFormat, not
// byte-identical) and ADR 08 Amendments 2026-11-18 (decision 10 (a): the formatter over NumberFormatInfo with the rounding helper).
// The steps are ECMA-402's: SetNumberFormatDigitOptions in the constructor, then PartitionNumberPattern (ComputeExponent,
// FormatNumericToString with ToRawPrecision and ToRawFixed, the sign and the style's pattern). The rounding works on the exact
// decimal value of the double's shortest round-trip digits, as ICU does. Patterns come from the culture's NumberFormatInfo
// (.NET's pattern numbers, every pattern space written U+00A0) and the data NumberFieldCultureData adds.
/// <summary>Formats numbers under a culture and a set of number format options.</summary>
internal sealed class NumberFormatter
{
    private const char NoBreakSpace = ' ';
    private const int MaxFractionDigitsLimit = 100;
    private const int MaxSignificantDigitsLimit = 21;
    private const int MaxIntegerDigitsLimit = 21;
    private const int CompactMaxExponent = 12;

    private static readonly int[] s_roundingIncrements = [1, 2, 5, 10, 20, 25, 50, 100, 200, 250, 500, 1000, 2000, 2500, 5000];
    private static readonly string[] s_compactSuffixes = [string.Empty, "K", "M", "B", "T"];
    private static readonly ConcurrentDictionary<(string Culture, NumberFormatOptions? Options), NumberFormatter> s_cache = new();

    [ThreadStatic]
    private static CultureInfo? s_lastCulture;

    [ThreadStatic]
    private static NumberFormatOptions? s_lastOptions;

    [ThreadStatic]
    private static NumberFormatter? s_last;

    [ThreadStatic]
    private static StringBuilder? s_builder;

    private readonly CultureInfo culture;
    private readonly NumberFormatInfo info;
    private readonly NumberFormatStyle style;
    private readonly NumberFormatNotation notation;
    private readonly NumberFormatSignDisplay signDisplay;
    private readonly bool accounting;
    private readonly bool useGrouping;
    private readonly int minimumIntegerDigits;
    private readonly int minimumFractionDigits;
    private readonly int maximumFractionDigits;
    private readonly int minimumSignificantDigits;
    private readonly int maximumSignificantDigits;
    private readonly RoundingType roundingType;
    private readonly int roundingIncrement;
    private readonly NumberFormatRoundingMode roundingMode;
    private readonly string[]? nativeDigits;
    private readonly string exponentSeparator;
    private readonly int minimumGrouping;
    private readonly string? currencyText;
    private readonly bool currencyName;
    private readonly string? unitSeparator;
    private readonly string? unitText;
    private NumberLocaleDetails? localeDetails;
    private string[]? unitParts;

    private NumberFormatter(CultureInfo culture, NumberFormatOptions? options)
    {
        this.culture = culture;
        info = culture.NumberFormat;
        options ??= new NumberFormatOptions();
        style = options.Style;
        notation = options.Notation;
        signDisplay = options.SignDisplay;
        accounting = options.CurrencySign == NumberFormatCurrencySign.Accounting;
        useGrouping = options.UseGrouping;
        roundingMode = options.RoundingMode ?? NumberFormatRoundingMode.HalfExpand;
        nativeDigits = NumberCultureData.NativeDigits(info);
        exponentSeparator = NumberCultureData.ExponentSeparator(nativeDigits);
        minimumGrouping = notation == NumberFormatNotation.Compact ? 2 : NumberCultureData.MinimumGroupingDigits(culture);

        int currencyDigits = 0;
        if (style == NumberFormatStyle.Currency)
        {
            string code = options.Currency is { Length: 3 } c && IsAsciiLetters(c)
                ? c.ToUpperInvariant()
                : throw new ArgumentException($"Iodine: `NumberFormatOptions.Currency` must be a three-letter ISO 4217 code when `Style` is `{nameof(NumberFormatStyle.Currency)}`; it is `{options.Currency ?? "null"}`.", nameof(options));
            currencyDigits = NumberCultureData.CurrencyDigits(code);
            currencyText = NumberCultureData.CurrencyText(culture, code, options.CurrencyDisplay);
            currencyName = options.CurrencyDisplay == NumberFormatCurrencyDisplay.Name;
        }
        else if (style == NumberFormatStyle.Unit)
        {
            NumberFormatUnit unit = options.Unit
                ?? throw new ArgumentException($"Iodine: `NumberFormatOptions.Unit` is required when `Style` is `{nameof(NumberFormatStyle.Unit)}`.", nameof(options));
            if (unit != NumberFormatUnit.Percent)
            {
                (unitSeparator, unitText) = NumberCultureData.UnitText(culture, unit);
            }
        }

        // SetNumberFormatDigitOptions (ECMA-402 15.1.3) with the defaults of InitializeNumberFormat.
        int defaultMinimumFraction = style == NumberFormatStyle.Currency ? currencyDigits : 0;
        int defaultMaximumFraction = style switch
        {
            NumberFormatStyle.Currency => Math.Max(currencyDigits, defaultMinimumFraction),
            NumberFormatStyle.Percent => defaultMinimumFraction,
            _ => Math.Max(defaultMinimumFraction, 3),
        };

        minimumIntegerDigits = Range(options.MinimumIntegerDigits, 1, MaxIntegerDigitsLimit, 1, nameof(NumberFormatOptions.MinimumIntegerDigits));
        roundingIncrement = options.RoundingIncrement ?? 1;
        if (Array.IndexOf(s_roundingIncrements, roundingIncrement) < 0)
        {
            throw new ArgumentException($"Iodine: `NumberFormatOptions.RoundingIncrement` must be one of 1, 2, 5, 10, 20, 25, 50, 100, 200, 250, 500, 1000, 2000, 2500 and 5000; it is `{roundingIncrement}`.", nameof(options));
        }

        if (roundingIncrement != 1)
        {
            defaultMaximumFraction = defaultMinimumFraction;
        }

        NumberFormatRoundingPriority priority = options.RoundingPriority ?? NumberFormatRoundingPriority.Auto;
        bool hasSignificant = options.MinimumSignificantDigits is not null || options.MaximumSignificantDigits is not null;
        bool hasFraction = options.MinimumFractionDigits is not null || options.MaximumFractionDigits is not null;
        bool needSignificant = true;
        bool needFraction = true;
        if (priority == NumberFormatRoundingPriority.Auto)
        {
            needSignificant = hasSignificant;
            if (needSignificant || (!hasFraction && notation == NumberFormatNotation.Compact))
            {
                needFraction = false;
            }
        }

        if (needSignificant)
        {
            if (hasSignificant)
            {
                minimumSignificantDigits = Range(options.MinimumSignificantDigits, 1, MaxSignificantDigitsLimit, 1, nameof(NumberFormatOptions.MinimumSignificantDigits));
                maximumSignificantDigits = Range(options.MaximumSignificantDigits, minimumSignificantDigits, MaxSignificantDigitsLimit, MaxSignificantDigitsLimit, nameof(NumberFormatOptions.MaximumSignificantDigits));
            }
            else
            {
                minimumSignificantDigits = 1;
                maximumSignificantDigits = MaxSignificantDigitsLimit;
            }
        }

        if (needFraction)
        {
            if (hasFraction)
            {
                int? minimum = options.MinimumFractionDigits;
                int? maximum = options.MaximumFractionDigits;
                Check(minimum, 0, MaxFractionDigitsLimit, nameof(NumberFormatOptions.MinimumFractionDigits));
                Check(maximum, 0, MaxFractionDigitsLimit, nameof(NumberFormatOptions.MaximumFractionDigits));
                if (minimum is null)
                {
                    minimum = Math.Min(defaultMinimumFraction, maximum!.Value);
                }
                else if (maximum is null)
                {
                    maximum = Math.Max(defaultMaximumFraction, minimum.Value);
                }
                else if (minimum > maximum)
                {
                    throw new ArgumentException($"Iodine: `NumberFormatOptions.MinimumFractionDigits` ({minimum}) is greater than `MaximumFractionDigits` ({maximum}).", nameof(options));
                }

                minimumFractionDigits = minimum.Value;
                maximumFractionDigits = maximum!.Value;
            }
            else
            {
                minimumFractionDigits = defaultMinimumFraction;
                maximumFractionDigits = defaultMaximumFraction;
            }
        }

        if (!needSignificant && !needFraction)
        {
            minimumFractionDigits = 0;
            maximumFractionDigits = 0;
            minimumSignificantDigits = 1;
            maximumSignificantDigits = 2;
            roundingType = RoundingType.MorePrecision;
        }
        else if (priority == NumberFormatRoundingPriority.Auto)
        {
            roundingType = needSignificant ? RoundingType.SignificantDigits : RoundingType.FractionDigits;
        }
        else
        {
            roundingType = priority == NumberFormatRoundingPriority.MorePrecision ? RoundingType.MorePrecision : RoundingType.LessPrecision;
        }

        if (roundingIncrement != 1)
        {
            if (roundingType != RoundingType.FractionDigits)
            {
                throw new ArgumentException("Iodine: `NumberFormatOptions.RoundingIncrement` other than 1 cannot be combined with significant digits.", nameof(options));
            }

            if (maximumFractionDigits != minimumFractionDigits)
            {
                throw new ArgumentException("Iodine: `NumberFormatOptions.RoundingIncrement` other than 1 requires equal `MinimumFractionDigits` and `MaximumFractionDigits`.", nameof(options));
            }
        }
    }

    /// <summary>Gets the culture the formatter formats under.</summary>
    public CultureInfo Culture => culture;

    // UTL-I18N-06/07: one instance per (culture name, options), the stringified locale and the options compared by value; the
    // thread's last pair is kept by reference, so a part that formats again with the same instances does no lookup.
    /// <summary>Gets the formatter of a culture and a set of options.</summary>
    /// <param name="culture">The culture, or <see langword="null"/> for <see cref="CultureInfo.CurrentCulture"/>.</param>
    /// <param name="options">The options, or <see langword="null"/> for the defaults.</param>
    /// <returns>The cached formatter.</returns>
    /// <exception cref="ArgumentException">The options are invalid, such as a currency style without a currency.</exception>
    public static NumberFormatter Get(CultureInfo? culture, NumberFormatOptions? options)
    {
        culture ??= CultureInfo.CurrentCulture;
        if (s_last is not null && ReferenceEquals(s_lastCulture, culture) && ReferenceEquals(s_lastOptions, options))
        {
            return s_last;
        }

        NumberFormatter formatter = s_cache.GetOrAdd((culture.Name, options), static (key, culture) => new NumberFormatter(culture, key.Options), culture);
        s_lastCulture = culture;
        s_lastOptions = options;
        s_last = formatter;
        return formatter;
    }

    // UTL-I18N-05, NUM-I18N-01.
    /// <summary>Formats a number, or returns the empty string for <see langword="null"/>.</summary>
    /// <param name="value">The number, or <see langword="null"/>.</param>
    /// <param name="culture">The culture, or <see langword="null"/> for <see cref="CultureInfo.CurrentCulture"/>.</param>
    /// <param name="options">The options, or <see langword="null"/> for the defaults.</param>
    /// <returns>The formatted text, or <see cref="string.Empty"/>.</returns>
    public static string FormatNumber(double? value, CultureInfo? culture, NumberFormatOptions? options)
    {
        return value is double number ? Get(culture, options).Format(number) : string.Empty;
    }

    /// <summary>Formats a number.</summary>
    /// <param name="value">The number.</param>
    /// <returns>The formatted text.</returns>
    public string Format(double value)
    {
        StringBuilder builder = s_builder ??= new StringBuilder(32);
        builder.Clear();
        var output = new Output(builder, null);
        Write(value, ref output);
        return builder.ToString();
    }

    /// <summary>Formats a number into its typed pieces.</summary>
    /// <param name="value">The number.</param>
    /// <returns>The pieces, in order.</returns>
    public List<NumberFormatPart> FormatToParts(double value)
    {
        var parts = new List<NumberFormatPart>();
        var output = new Output(new StringBuilder(32), parts);
        Write(value, ref output);
        return parts;
    }

    // NUM-I18N-07: the last value of each part type of formatToParts(11111.1), with the decimal taken from the plain culture's
    // formatToParts(0.1).
    /// <summary>Gets the culture's symbols as this formatter renders them.</summary>
    /// <returns>The locale details.</returns>
    public NumberLocaleDetails LocaleDetails()
    {
        if (localeDetails is not null)
        {
            return localeDetails;
        }

        var details = new NumberLocaleDetails();
        foreach (NumberFormatPart part in FormatToParts(NumberLocaleDetails.SampleNumber))
        {
            details.Set(part.Type, part.Value);
        }

        foreach (NumberFormatPart part in Get(culture, null).FormatToParts(0.1))
        {
            if (part.Type == NumberFormatPartType.Decimal)
            {
                details.Decimal = part.Value;
            }
        }

        details.Decimal ??= ".";
        localeDetails = details;
        return details;
    }

    // NUM-I18N-12: every unit part of formatToParts(1).
    /// <summary>Gets the unit texts this formatter renders.</summary>
    /// <returns>The unit texts, empty when the style renders none.</returns>
    public string[] UnitParts()
    {
        if (unitParts is not null)
        {
            return unitParts;
        }

        var units = new List<string>();
        foreach (NumberFormatPart part in FormatToParts(1))
        {
            if (part.Type == NumberFormatPartType.Unit)
            {
                units.Add(part.Value);
            }
        }

        unitParts = [.. units];
        return unitParts;
    }

    private static int Range(int? value, int minimum, int maximum, int fallback, string name)
    {
        Check(value, minimum, maximum, name);
        return value ?? fallback;
    }

    private static void Check(int? value, int minimum, int maximum, string name)
    {
        if (value is int v && (v < minimum || v > maximum))
        {
            throw new ArgumentException($"Iodine: `NumberFormatOptions.{name}` must be between {minimum} and {maximum}; it is `{v}`.", "options");
        }
    }

    private static bool IsAsciiLetters(string text)
    {
        foreach (char c in text)
        {
            if (!char.IsAsciiLetter(c))
            {
                return false;
            }
        }

        return true;
    }

    // PartitionNumberPattern.
    private void Write(double value, ref Output output)
    {
        bool negative = double.IsNegative(value) && !double.IsNaN(value);
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            WritePattern(ref output, negative ? Sign.Minus : Sign.None, default, double.IsNaN(value) ? "NaN" : "∞");
            return;
        }

        DecimalNumber x = DecimalRounding.FromDouble(value);
        if (style == NumberFormatStyle.Percent)
        {
            x = x.Shift(2);
        }

        int exponent = 0;
        Formatted formatted;
        if (notation == NumberFormatNotation.Standard)
        {
            formatted = FormatNumericToString(x, negative);
        }
        else
        {
            (exponent, formatted) = ComputeExponent(x, negative);
        }

        bool zero = formatted.Rounded.Coefficient.IsZero;
        Sign sign = signDisplay switch
        {
            NumberFormatSignDisplay.Never => Sign.None,
            NumberFormatSignDisplay.Always => negative ? Sign.Minus : Sign.Plus,
            NumberFormatSignDisplay.ExceptZero => zero ? Sign.None : negative ? Sign.Minus : Sign.Plus,
            NumberFormatSignDisplay.Negative => negative && !zero ? Sign.Minus : Sign.None,
            _ => negative ? Sign.Minus : Sign.None,
        };

        WritePattern(ref output, sign, new Body(formatted, exponent), null);
    }

    // ComputeExponent and ComputeExponentForMagnitude.
    private (int Exponent, Formatted Result) ComputeExponent(DecimalNumber x, bool negative)
    {
        if (x.Coefficient.IsZero)
        {
            return (0, FormatNumericToString(x, negative));
        }

        int magnitude = x.Magnitude;
        int exponent = ExponentForMagnitude(magnitude);
        Formatted result = FormatNumericToString(x.Shift(-exponent), negative);
        if (result.Rounded.Coefficient.IsZero || result.Rounded.Magnitude == magnitude - exponent)
        {
            return (exponent, result);
        }

        exponent = ExponentForMagnitude(magnitude + 1);
        return (exponent, FormatNumericToString(x.Shift(-exponent), negative));
    }

    private int ExponentForMagnitude(int magnitude)
    {
        if (notation == NumberFormatNotation.Scientific)
        {
            return magnitude;
        }

        // Compact: English short scale (thousands, millions, billions, trillions).
        return magnitude < 3 ? 0 : Math.Min(magnitude / 3 * 3, CompactMaxExponent);
    }

    // FormatNumericToString (without the sign, which the pattern writes).
    private Formatted FormatNumericToString(DecimalNumber x, bool negative)
    {
        UnsignedRounding rounding = DecimalRounding.ToUnsigned(roundingMode, negative);
        Formatted result;
        switch (roundingType)
        {
            case RoundingType.SignificantDigits:
                result = ToRawPrecision(x, minimumSignificantDigits, maximumSignificantDigits, rounding);
                break;
            case RoundingType.FractionDigits:
                result = ToRawFixed(x, minimumFractionDigits, maximumFractionDigits, roundingIncrement, rounding);
                break;
            default:
                Formatted significant = ToRawPrecision(x, minimumSignificantDigits, maximumSignificantDigits, rounding);
                Formatted fixedResult = ToRawFixed(x, minimumFractionDigits, maximumFractionDigits, roundingIncrement, rounding);
                bool significantIsMorePrecise = significant.RoundingMagnitude <= fixedResult.RoundingMagnitude;
                result = roundingType == RoundingType.MorePrecision
                    ? (significantIsMorePrecise ? significant : fixedResult)
                    : (significantIsMorePrecise ? fixedResult : significant);
                break;
        }

        if (result.Integer.Length < minimumIntegerDigits)
        {
            result = result with { Integer = result.Integer.PadLeft(minimumIntegerDigits, '0') };
        }

        return result;
    }

    private static Formatted ToRawPrecision(DecimalNumber x, int minimumPrecision, int maximumPrecision, UnsignedRounding rounding)
    {
        int p = maximumPrecision;
        string digits;
        int e;
        DecimalNumber rounded;
        if (x.Coefficient.IsZero)
        {
            digits = new string('0', p);
            e = 0;
            rounded = new DecimalNumber(BigInteger.Zero, 1 - p);
        }
        else
        {
            e = x.Magnitude;
            rounded = DecimalRounding.Round(x, e - p + 1, 1, rounding);
            if (DecimalNumber.DigitCount(rounded.Coefficient) > p)
            {
                // Rounded up to the next power of ten: n = 10^(p-1) one magnitude higher.
                e++;
                rounded = new DecimalNumber(BigInteger.Pow(10, p - 1), e - p + 1);
            }

            digits = rounded.Coefficient.ToString(CultureInfo.InvariantCulture);
        }

        string integer;
        string fraction;
        if (e >= p - 1)
        {
            integer = digits + new string('0', e - p + 1);
            fraction = string.Empty;
        }
        else if (e >= 0)
        {
            integer = digits[..(e + 1)];
            fraction = digits[(e + 1)..];
        }
        else
        {
            integer = "0";
            fraction = new string('0', -(e + 1)) + digits;
        }

        if (fraction.Length > 0 && maximumPrecision > minimumPrecision)
        {
            fraction = TrimZeros(fraction, maximumPrecision - minimumPrecision);
        }

        return new Formatted(integer, fraction, rounded, e - p + 1);
    }

    private static Formatted ToRawFixed(DecimalNumber x, int minimumFraction, int maximumFraction, int increment, UnsignedRounding rounding)
    {
        int f = maximumFraction;
        DecimalNumber rounded = DecimalRounding.Round(x, -f, increment, rounding);
        string digits = rounded.Coefficient.ToString(CultureInfo.InvariantCulture);
        string integer;
        string fraction;
        if (f == 0)
        {
            integer = digits;
            fraction = string.Empty;
        }
        else
        {
            if (digits.Length <= f)
            {
                digits = new string('0', f + 1 - digits.Length) + digits;
            }

            integer = digits[..^f];
            fraction = digits[^f..];
        }

        fraction = TrimZeros(fraction, maximumFraction - minimumFraction);
        return new Formatted(integer, fraction, rounded, -f);
    }

    private static string TrimZeros(string fraction, int cut)
    {
        int end = fraction.Length;
        while (cut > 0 && end > 0 && fraction[end - 1] == '0')
        {
            end--;
            cut--;
        }

        return end == fraction.Length ? fraction : fraction[..end];
    }

    // The style's pattern with the sign, from the culture's .NET pattern numbers. Tokens: 'n' the number, '-' the sign,
    // '%' the percent sign, '$' the currency, ' ' a pattern space, '(' and ')' the accounting parentheses.
    private void WritePattern(ref Output output, Sign sign, Body body, string? special)
    {
        bool percentLike = style == NumberFormatStyle.Percent || (style == NumberFormatStyle.Unit && unitText is null);
        string pattern;
        if (percentLike)
        {
            pattern = sign == Sign.None ? PercentPositive(info.PercentPositivePattern) : PercentNegative(info.PercentNegativePattern);
        }
        else if (style == NumberFormatStyle.Currency && currencyName)
        {
            // CLDR's currency-name pattern: the number, a space and the name, as a unit is written.
            pattern = (sign == Sign.None ? "n" : NumberNegative(info.NumberNegativePattern)) + " $";
        }
        else if (style == NumberFormatStyle.Currency)
        {
            pattern = sign switch
            {
                Sign.None => CurrencyPositive(info.CurrencyPositivePattern),
                Sign.Minus when accounting => CurrencyAccounting(info.CurrencyPositivePattern),
                _ => CurrencyNegative(info.CurrencyNegativePattern),
            };
            pattern = SpaceCurrency(pattern);
        }
        else
        {
            pattern = sign == Sign.None ? "n" : NumberNegative(info.NumberNegativePattern);
        }

        if (sign == Sign.Plus && !pattern.Contains('-', StringComparison.Ordinal))
        {
            pattern = "-" + pattern.Replace("(", string.Empty, StringComparison.Ordinal).Replace(")", string.Empty, StringComparison.Ordinal);
        }

        foreach (char token in pattern)
        {
            switch (token)
            {
                case 'n':
                    if (special is not null)
                    {
                        output.Add(special == "NaN" ? NumberFormatPartType.Nan : NumberFormatPartType.Infinity, special);
                    }
                    else
                    {
                        WriteBody(ref output, body);
                    }

                    break;
                case '-':
                    if (sign == Sign.Minus)
                    {
                        output.Add(NumberFormatPartType.MinusSign, info.NegativeSign);
                    }
                    else if (sign == Sign.Plus)
                    {
                        output.Add(NumberFormatPartType.PlusSign, info.PositiveSign);
                    }

                    break;
                case '%':
                    output.Add(NumberFormatPartType.PercentSign, info.PercentSymbol);
                    break;
                case '$':
                    output.Add(NumberFormatPartType.Currency, currencyText!);
                    break;
                case ' ':
                    output.Add(NumberFormatPartType.Literal, " ");
                    break;
                default:
                    output.Add(NumberFormatPartType.Literal, token.ToString());
                    break;
            }
        }

        if (unitText is not null)
        {
            if (unitSeparator!.Length > 0)
            {
                output.Add(NumberFormatPartType.Literal, unitSeparator);
            }

            output.Add(NumberFormatPartType.Unit, unitText);
        }
    }

    private void WriteBody(ref Output output, Body body)
    {
        string integer = body.Result.Integer;
        int[] sizes = info.NumberGroupSizes;
        int primary = sizes.Length > 0 ? sizes[0] : 0;
        if (useGrouping && primary > 0 && integer.Length - primary >= minimumGrouping)
        {
            WriteGrouped(ref output, integer, sizes);
        }
        else
        {
            output.AddDigits(NumberFormatPartType.Integer, integer, nativeDigits);
        }

        if (body.Result.Fraction.Length > 0)
        {
            output.Add(NumberFormatPartType.Decimal, info.NumberDecimalSeparator);
            output.AddDigits(NumberFormatPartType.Fraction, body.Result.Fraction, nativeDigits);
        }

        if (notation == NumberFormatNotation.Scientific)
        {
            output.Add(NumberFormatPartType.ExponentSeparator, exponentSeparator);
            if (body.Exponent < 0)
            {
                output.Add(NumberFormatPartType.ExponentMinusSign, info.NegativeSign);
            }

            output.AddDigits(NumberFormatPartType.ExponentInteger, Math.Abs(body.Exponent).ToString(CultureInfo.InvariantCulture), nativeDigits);
        }
        else if (notation == NumberFormatNotation.Compact && body.Exponent > 0)
        {
            output.Add(NumberFormatPartType.Compact, s_compactSuffixes[body.Exponent / 3]);
        }
    }

    // .NET's NumberGroupSizes: the first size groups the lowest digits, each next size the following group, the last size
    // repeats, and a last size of 0 leaves the remaining digits ungrouped.
    private void WriteGrouped(ref Output output, string integer, int[] sizes)
    {
        Span<int> starts = stackalloc int[integer.Length + 1];
        int count = 0;
        int end = integer.Length;
        int index = 0;
        while (end > 0)
        {
            int size = sizes[Math.Min(index, sizes.Length - 1)];
            if (size == 0)
            {
                break;
            }

            end -= size;
            if (end <= 0)
            {
                break;
            }

            starts[count++] = end;
            index++;
        }

        int from = 0;
        for (int i = count - 1; i >= 0; i--)
        {
            output.AddDigits(NumberFormatPartType.Integer, integer[from..starts[i]], nativeDigits);
            output.Add(NumberFormatPartType.Group, info.NumberGroupSeparator);
            from = starts[i];
        }

        output.AddDigits(NumberFormatPartType.Integer, integer[from..], nativeDigits);
    }

    // CLDR currency spacing: a symbol whose character next to the number is a letter (a code such as EUR, or CHF) is separated
    // from the digits by a no-break space when the pattern has none.
    private string SpaceCurrency(string pattern)
    {
        string text = currencyText!;
        if (text.Length == 0)
        {
            return pattern;
        }

        int at = pattern.IndexOf('$', StringComparison.Ordinal);
        int number = pattern.IndexOf('n', StringComparison.Ordinal);
        bool before = at < number;
        char adjacent = before ? text[^1] : text[0];
        if (!char.IsLetter(adjacent))
        {
            return pattern;
        }

        int between = before ? at + 1 : number + 1;
        int last = before ? number : at;
        for (int i = between; i < last; i++)
        {
            if (pattern[i] == ' ')
            {
                return pattern;
            }
        }

        return before ? pattern.Insert(at + 1, " ") : pattern.Insert(at, " ");
    }

    private static string NumberNegative(int pattern) => pattern switch
    {
        0 => "(n)",
        2 => "- n",
        3 => "n-",
        4 => "n -",
        _ => "-n",
    };

    private static string PercentPositive(int pattern) => pattern switch
    {
        0 => "n %",
        2 => "%n",
        3 => "% n",
        _ => "n%",
    };

    private static string PercentNegative(int pattern) => pattern switch
    {
        0 => "-n %",
        2 => "-%n",
        3 => "%-n",
        4 => "%n-",
        5 => "n-%",
        6 => "n%-",
        7 => "-% n",
        8 => "n %-",
        9 => "% n-",
        10 => "% -n",
        11 => "n- %",
        _ => "-n%",
    };

    private static string CurrencyPositive(int pattern) => pattern switch
    {
        1 => "n$",
        2 => "$ n",
        3 => "n $",
        _ => "$n",
    };

    private static string CurrencyNegative(int pattern) => pattern switch
    {
        0 => "($n)",
        2 => "$-n",
        3 => "$n-",
        4 => "(n$)",
        5 => "-n$",
        6 => "n-$",
        7 => "n$-",
        8 => "-n $",
        9 => "-$ n",
        10 => "n $-",
        11 => "$ n-",
        12 => "$ -n",
        13 => "n- $",
        14 => "($ n)",
        15 => "(n $)",
        16 => "$- n",
        _ => "-$n",
    };

    // The accounting form of a negative amount: the positive pattern in parentheses.
    private static string CurrencyAccounting(int positivePattern) => "(" + CurrencyPositive(positivePattern) + ")";

    private enum RoundingType
    {
        FractionDigits,
        SignificantDigits,
        MorePrecision,
        LessPrecision,
    }

    private readonly record struct Formatted(string Integer, string Fraction, DecimalNumber Rounded, int RoundingMagnitude);

    private readonly record struct Body(Formatted Result, int Exponent);

    private enum Sign
    {
        None,
        Minus,
        Plus,
    }

    // Appends to the text and, when parts are requested, records each piece.
    private readonly ref struct Output(StringBuilder builder, List<NumberFormatPart>? parts)
    {
        public void Add(NumberFormatPartType type, string value)
        {
            builder.Append(value);
            parts?.Add(new NumberFormatPart(type, value));
        }

        public void AddDigits(NumberFormatPartType type, string digits, string[]? native)
        {
            if (native is null)
            {
                Add(type, digits);
                return;
            }

            int start = builder.Length;
            foreach (char c in digits)
            {
                builder.Append(native[c - '0']);
            }

            parts?.Add(new NumberFormatPart(type, builder.ToString(start, builder.Length - start)));
        }
    }
}
