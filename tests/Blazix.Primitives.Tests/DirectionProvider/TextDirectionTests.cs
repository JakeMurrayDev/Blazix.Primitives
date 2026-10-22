using System.Reflection;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Tests;

/// <summary>
/// L1: UTL-I18N-02 as Base UI's DirectionProvider.spec.tsx checks it, at the type level (the reading direction is a
/// two-member enum, the provider's parameter is of that type and defaults to left to right; a value outside it does not
/// compile), and the literals the composite module receives for the two shared enums it reads.
/// </summary>
public sealed class TextDirectionTests
{
    [Fact(DisplayName = "TextDirection is ltr or rtl and DirectionProvider.Direction defaults to ltr")]
    [Trait("Requirement", "UTL-I18N-02")]
    public void TextDirection_is_ltr_or_rtl_and_the_provider_defaults_to_ltr()
    {
        // Base UI's `@ts-expect-error <DirectionProvider direction="vertical" />` has its C# counterpart in the compiler:
        // `Direction="TextDirection.Vertical"` does not exist and a string does not convert.
        TextDirection rtl = TextDirection.Rtl;

        Assert.Equal(["Ltr", "Rtl"], Enum.GetNames<TextDirection>());
        Assert.Equal(TextDirection.Ltr, new DirectionProvider().Direction);
        Assert.NotEqual(TextDirection.Ltr, rtl);

        PropertyInfo direction = typeof(DirectionProvider).GetProperty(nameof(DirectionProvider.Direction))!;
        Assert.Equal(typeof(TextDirection), direction.PropertyType);
        Assert.NotNull(direction.GetCustomAttribute<ParameterAttribute>());
        Assert.Equal(typeof(RenderFragment), typeof(DirectionProvider).GetProperty(nameof(DirectionProvider.ChildContent))!.PropertyType);
    }

    [Fact(DisplayName = "TextDirection maps to the literals ltr and rtl")]
    [Trait("Requirement", "CMP-I18N-01")]
    public void TextDirection_maps_to_its_literals()
    {
        Assert.Equal("ltr", CompositeLiterals.ToLiteral(TextDirection.Ltr));
        Assert.Equal("rtl", CompositeLiterals.ToLiteral(TextDirection.Rtl));
        Assert.Throws<ArgumentOutOfRangeException>(() => CompositeLiterals.ToLiteral((TextDirection)2));
    }

    [Fact(DisplayName = "an orientation maps to its literal and no orientation to both")]
    [Trait("Requirement", "CMP-API-02")]
    public void Orientation_maps_to_its_literal_and_null_to_both()
    {
        Assert.Equal("horizontal", CompositeLiterals.ToLiteral(Orientation.Horizontal));
        Assert.Equal("vertical", CompositeLiterals.ToLiteral(Orientation.Vertical));
        Assert.Equal("both", CompositeLiterals.ToLiteral((Orientation?)null));
        Assert.Throws<ArgumentOutOfRangeException>(() => CompositeLiterals.ToLiteral((Orientation?)(Orientation)5));
    }
}
