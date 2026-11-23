namespace Blazix.Primitives;

// ADR 08 §3's Enums row and Amendments 2026-11-18 (decision 10 (a)): a literal-valued option of NumberFormatOptions, the
// PascalCase of its Intl.NumberFormatOptions literals, beside its first consumer and named in ADR 08 §3's companion table.
/// <summary>Specifies the unit of a number formatted with <see cref="NumberFormatStyle.Unit"/>.</summary>
public enum NumberFormatUnit
{
    /// <summary>Percent, shown <c>12%</c>; unlike <see cref="NumberFormatStyle.Percent"/>, the value is not multiplied by 100.</summary>
    Percent,

    /// <summary>Kilograms, shown <c>12 kg</c>.</summary>
    Kilogram,

    /// <summary>Grams, shown <c>12 g</c>.</summary>
    Gram,

    /// <summary>Meters, shown <c>12 m</c>.</summary>
    Meter,

    /// <summary>Kilometers, shown <c>12 km</c>.</summary>
    Kilometer,

    /// <summary>Centimeters, shown <c>12 cm</c>.</summary>
    Centimeter,

    /// <summary>Millimeters, shown <c>12 mm</c>.</summary>
    Millimeter,

    /// <summary>Liters, shown <c>12 L</c>.</summary>
    Liter,

    /// <summary>Milliliters, shown <c>12 mL</c>.</summary>
    Milliliter,

    /// <summary>Kilometers per hour, shown <c>12 km/h</c>.</summary>
    KilometerPerHour,

    /// <summary>Meters per second, shown <c>12 m/s</c>.</summary>
    MeterPerSecond,

    /// <summary>Degrees Celsius, shown <c>12°C</c>.</summary>
    Celsius,

    /// <summary>Degrees Fahrenheit, shown <c>12°F</c>.</summary>
    Fahrenheit,

    /// <summary>Kilobytes, shown <c>12 kB</c>.</summary>
    Kilobyte,

    /// <summary>Megabytes, shown <c>12 MB</c>.</summary>
    Megabyte,

    /// <summary>Gigabytes, shown <c>12 GB</c>.</summary>
    Gigabyte,
}
