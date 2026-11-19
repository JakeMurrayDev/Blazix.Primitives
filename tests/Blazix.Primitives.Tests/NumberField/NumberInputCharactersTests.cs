using System.Globalization;
using Blazix.Primitives.Utilities;

namespace Blazix.Primitives.Tests;

// NUM-KEY-07 and NUM-EVT-15: NumberFieldRoot.tsx's getAllowedNonNumericKeys (:152-206) and NumberFieldInput.tsx's character
// test of a change (:247-258), which C# computes for the module's keydown gate and runs again on every text it is sent
// (decision 7 (a); issue 277, 6b). Base UI tests them through the input; these are port titles over the helper.
/// <summary>The characters a number field's input accepts.</summary>
public sealed class NumberInputCharactersTests
{
    private static readonly CultureInfo s_enUs = CultureInfo.GetCultureInfo("en-US");

    [Fact(DisplayName = "port: the default set holds the base symbols, the locale's separators and the plus signs, and the minus signs only when negatives are valid")]
    [Trait("Requirement", "NUM-KEY-07")]
    public void The_default_set()
    {
        string positive = NumberInputCharacters.Allowed(s_enUs, null, allowMinus: false);
        string signed = NumberInputCharacters.Allowed(s_enUs, null, allowMinus: true);

        foreach (char c in ".,．，٫٬+＋﹢")
        {
            Assert.Contains(c, positive);
        }

        Assert.DoesNotContain('-', positive);
        foreach (char c in "-−－‒–—﹣")
        {
            Assert.Contains(c, signed);
        }

        Assert.Equal(positive.Length, positive.Distinct().Count());
    }

    [Fact(DisplayName = "port: the set takes every character the format writes except the digits and compact suffixes")]
    [Trait("Requirement", "NUM-KEY-07")]
    public void The_set_takes_every_character_the_format_writes()
    {
        string currency = NumberInputCharacters.Allowed(s_enUs, new() { Style = NumberFormatStyle.Currency, Currency = "USD" }, allowMinus: false);
        string speed = NumberInputCharacters.Allowed(s_enUs, new() { Style = NumberFormatStyle.Unit, Unit = NumberFormatUnit.KilometerPerHour }, allowMinus: false);
        string compact = NumberInputCharacters.Allowed(s_enUs, new() { Notation = NumberFormatNotation.Compact }, allowMinus: false);

        Assert.Contains('$', currency);
        foreach (char c in "km/h ")
        {
            Assert.Contains(c, speed);
        }

        Assert.DoesNotContain('K', compact);
        Assert.DoesNotContain('1', currency);
    }

    [Fact(DisplayName = "port: percent formats accept every percent and permille variant")]
    [Trait("Requirement", "NUM-KEY-07")]
    public void Percent_formats_accept_every_percent_and_permille_variant()
    {
        string percent = NumberInputCharacters.Allowed(s_enUs, new() { Style = NumberFormatStyle.Percent }, allowMinus: false);
        string plain = NumberInputCharacters.Allowed(s_enUs, null, allowMinus: false);

        foreach (char c in "%٪％﹪‰؉")
        {
            Assert.Contains(c, percent);
        }

        Assert.DoesNotContain('%', plain);
    }

    [Fact(DisplayName = "port: a text is allowed when every character is a numeral, a minus variant, in the set or a format control")]
    [Trait("Requirement", "NUM-EVT-15")]
    [Trait("Requirement", "NUM-EDGE-06")]
    public void A_text_is_allowed_when_every_character_is_accepted()
    {
        string allowed = NumberInputCharacters.Allowed(s_enUs, null, allowMinus: false);

        Assert.True(NumberInputCharacters.IsAllowedText("1,234.5", allowed));
        Assert.True(NumberInputCharacters.IsAllowedText("−٥٠", allowed));
        Assert.True(NumberInputCharacters.IsAllowedText("‎12", allowed));
        Assert.False(NumberInputCharacters.IsAllowedText("12a", allowed));
        Assert.False(NumberInputCharacters.IsAllowedText("12%", allowed));
    }
}
