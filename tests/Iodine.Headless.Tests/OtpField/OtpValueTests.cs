namespace Iodine.Headless.Tests;

// otp-field/utils/otp.test.ts, the 19 titles verbatim over the C# helpers (OTP-STA-04..08, OTP-API-08, OTP-EDGE-07; issue
// 278, M6 track 7), and port titles for the ECMAScript classes and the code-point clamp (OTP-EDGE-12; §16 item 2,
// replicated; sheet D contradiction 12).
/// <summary>The normalization, replacement and removal of a one-time code's value.</summary>
public sealed class OtpValueTests
{
    [Fact(DisplayName = "removes whitespace from pasted values")]
    [Trait("Requirement", "OTP-STA-04")]
    public void Removes_whitespace_from_pasted_values()
    {
        Assert.Equal("12345", OtpValue.StripWhitespace(" 12 3\t4\n5 "));
    }

    [Fact(DisplayName = "returns an empty string when stripping nullish values")]
    [Trait("Requirement", "OTP-STA-04")]
    public void Returns_an_empty_string_when_stripping_nullish_values()
    {
        Assert.Equal(string.Empty, OtpValue.StripWhitespace(null));
    }

    [Fact(DisplayName = "normalizes, filters, and clamps numeric values")]
    [Trait("Requirement", "OTP-STA-04")]
    public void Normalizes_filters_and_clamps_numeric_values()
    {
        Assert.Equal("1234", OtpValue.Normalize("1a 2b34c56", 4, OtpFieldValidationType.Numeric));
    }

    [Fact(DisplayName = "normalizes alphabetic values")]
    [Trait("Requirement", "OTP-STA-05")]
    public void Normalizes_alphabetic_values()
    {
        Assert.Equal("abC", OtpValue.Normalize("1a 2b3C4", 6, OtpFieldValidationType.Alpha));
    }

    [Fact(DisplayName = "normalizes alphanumeric values")]
    [Trait("Requirement", "OTP-STA-05")]
    public void Normalizes_alphanumeric_values()
    {
        Assert.Equal("A1B2c3", OtpValue.Normalize("A1-B2 c3!", 6, OtpFieldValidationType.Alphanumeric));
    }

    [Fact(DisplayName = "returns an empty string for nullish input values")]
    [Trait("Requirement", "OTP-STA-04")]
    public void Returns_an_empty_string_for_nullish_input_values()
    {
        Assert.Equal(string.Empty, OtpValue.Normalize(null, 6, OtpFieldValidationType.Numeric));
        Assert.Equal(string.Empty, OtpValue.Normalize(null, 6, OtpFieldValidationType.Alpha));
    }

    [Fact(DisplayName = "uses custom normalization when validationType is none")]
    [Trait("Requirement", "OTP-STA-04")]
    public void Uses_custom_normalization_when_validation_type_is_none()
    {
        string result = OtpValue.Normalize("ab-12 cd", 6, OtpFieldValidationType.None, value => new string(value.Where(char.IsAsciiLetterOrDigit).ToArray()).ToUpperInvariant());

        Assert.Equal("AB12CD", result);
    }

    [Fact(DisplayName = "applies custom normalization after built-in validation")]
    [Trait("Requirement", "OTP-API-08")]
    public void Applies_custom_normalization_after_built_in_validation()
    {
        List<string> calls = [];
        string Normalize(string value)
        {
            calls.Add(value);
            return value.ToUpperInvariant();
        }

        Assert.Equal("AB12CD", OtpValue.Normalize("ab-12 cd!", 6, OtpFieldValidationType.Alphanumeric, Normalize));
        Assert.Equal(["ab12cd"], calls);
    }

    [Fact(DisplayName = "filters custom normalization output through built-in validation")]
    [Trait("Requirement", "OTP-API-08")]
    public void Filters_custom_normalization_output_through_built_in_validation()
    {
        Assert.Equal("12", OtpValue.Normalize("12", 6, OtpFieldValidationType.Numeric, value => value + "AB"));
    }

    [Fact(DisplayName = "clamps values after custom normalization")]
    [Trait("Requirement", "OTP-API-08")]
    public void Clamps_values_after_custom_normalization()
    {
        Assert.Equal("1234", OtpValue.Normalize("123456", 4, OtpFieldValidationType.Numeric, value => value + "789"));
    }

    [Fact(DisplayName = "returns an empty string for negative lengths")]
    [Trait("Requirement", "OTP-EDGE-07")]
    public void Returns_an_empty_string_for_negative_lengths()
    {
        Assert.Equal(string.Empty, OtpValue.Normalize("1234", -1, OtpFieldValidationType.None));
    }

    [Fact(DisplayName = "replaces values from the middle of the OTP")]
    [Trait("Requirement", "OTP-STA-07")]
    public void Replaces_values_from_the_middle_of_the_otp()
    {
        Assert.Equal("129956", OtpValue.Replace("123456", 2, "99", 6, OtpFieldValidationType.Numeric));
    }

    [Fact(DisplayName = "replaces values from the first slot")]
    [Trait("Requirement", "OTP-STA-07")]
    public void Replaces_values_from_the_first_slot()
    {
        Assert.Equal("993456", OtpValue.Replace("123456", 0, "99", 6, OtpFieldValidationType.Numeric));
    }

    [Fact(DisplayName = "replaces values at the last slot")]
    [Trait("Requirement", "OTP-STA-07")]
    public void Replaces_values_at_the_last_slot()
    {
        Assert.Equal("123459", OtpValue.Replace("123456", 5, "9", 6, OtpFieldValidationType.Numeric));
    }

    [Fact(DisplayName = "applies custom normalization when replacing OTP values")]
    [Trait("Requirement", "OTP-STA-07")]
    public void Applies_custom_normalization_when_replacing_otp_values()
    {
        Assert.Equal("12AB56", OtpValue.Replace("123456", 2, "ab", 6, OtpFieldValidationType.Alphanumeric, value => value.ToUpperInvariant()));
    }

    [Fact(DisplayName = "preserves suffix characters when custom normalization removes part of a middle replacement")]
    [Trait("Requirement", "OTP-STA-07")]
    public void Preserves_suffix_characters_when_custom_normalization_removes_part_of_a_middle_replacement()
    {
        string KeepZeroToThree(string value) => new(value.Where(c => c is >= '0' and <= '3').ToArray());

        Assert.Equal("1203", OtpValue.Replace("1303", 1, "29", 4, OtpFieldValidationType.Numeric, KeepZeroToThree));
    }

    [Fact(DisplayName = "removes a character from the first slot")]
    [Trait("Requirement", "OTP-STA-08")]
    public void Removes_a_character_from_the_first_slot()
    {
        Assert.Equal("234", OtpValue.Remove("1234", 0));
    }

    [Fact(DisplayName = "removes a character from the last slot")]
    [Trait("Requirement", "OTP-STA-08")]
    public void Removes_a_character_from_the_last_slot()
    {
        Assert.Equal("123", OtpValue.Remove("1234", 3));
    }

    [Fact(DisplayName = "leaves the value unchanged when removing an out-of-bounds index")]
    [Trait("Requirement", "OTP-STA-08")]
    public void Leaves_the_value_unchanged_when_removing_an_out_of_bounds_index()
    {
        Assert.Equal("1234", OtpValue.Remove("1234", 10));
    }

    [Fact(DisplayName = "port: whitespace is ECMAScript's \\s, Unicode spaces and the byte order mark included (OTP-STA-04)")]
    [Trait("Requirement", "OTP-STA-04")]
    public void Whitespace_is_ecmascripts_class()
    {
        string spaced = new(['1', (char)0x00A0, '2', (char)0x2007, '3', (char)0x3000, (char)0xFEFF, '4', (char)0x2028, (char)0x000B]);
        string zeroWidth = new(['1', (char)0x200B, '2']);

        Assert.Equal("1234", OtpValue.StripWhitespace(spaced));
        Assert.Equal(zeroWidth, OtpValue.StripWhitespace(zeroWidth));
    }

    [Fact(DisplayName = "port: the numeric class is ASCII digits only and the alphabetic class ASCII letters only (OTP-STA-05)")]
    [Trait("Requirement", "OTP-STA-05")]
    public void The_classes_are_ascii()
    {
        Assert.Equal("12", OtpValue.Normalize("1٣2", 6, OtpFieldValidationType.Numeric));
        Assert.Equal("ab", OtpValue.Normalize("aéb", 6, OtpFieldValidationType.Alpha));
        Assert.Equal("a1", OtpValue.Normalize("a١1", 6, OtpFieldValidationType.Alphanumeric));
    }

    [Fact(DisplayName = "port: the clamp counts code points, and a character outside the BMP is kept whole (OTP-EDGE-12)")]
    [Trait("Requirement", "OTP-EDGE-12")]
    public void The_clamp_counts_code_points()
    {
        Assert.Equal("a😀b", OtpValue.Normalize("a😀bc", 3, OtpFieldValidationType.None));
        Assert.Equal(4, "a😀b".Length);
        Assert.Equal("1", OtpValue.Normalize("1😀", 6, OtpFieldValidationType.Numeric));
    }

    [Fact(DisplayName = "port: normalization reports rejected characters, and whitespace removal is no rejection (OTP-STA-04)")]
    [Trait("Requirement", "OTP-STA-04")]
    public void Normalization_reports_rejected_characters()
    {
        Assert.Equal(("12", false), OtpValue.NormalizeWithDetails(" 1 2 ", 6, OtpFieldValidationType.Numeric));
        Assert.Equal(("12", true), OtpValue.NormalizeWithDetails("1a2", 6, OtpFieldValidationType.Numeric));
        Assert.Equal(("12", true), OtpValue.NormalizeWithDetails("123", 2, OtpFieldValidationType.Numeric));
        Assert.Equal(("1", true), OtpValue.NormalizeWithDetails("12", 6, OtpFieldValidationType.Numeric, value => value[..1]));
        Assert.Equal(("12", true), OtpValue.NormalizeWithDetails("12", 6, OtpFieldValidationType.Numeric, value => value + "x"));
        Assert.Equal(("12", false), OtpValue.NormalizeWithDetails("12", 6, null));
    }
}
