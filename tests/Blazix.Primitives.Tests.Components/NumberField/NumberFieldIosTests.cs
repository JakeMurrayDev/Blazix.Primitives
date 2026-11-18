using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// NumberFieldRoot.iOS.test.tsx (3 titles), NUM-STA-19, over a stubbed `readPlatformFlags` that reports iOS (Base UI mocks
// `platform.os.ios`; issue 277 design point 4 (a), settled by the orchestrator 2026-11-18). The flag reaches C# after the
// first interactive render, so the input renders `numeric` first and the iOS mode after the read (ADR 01's row, like
// MNU-A11Y-01's).
/// <summary>The number field's input mode on iOS.</summary>
public sealed class NumberFieldIosTests : IDisposable
{
    private readonly NumberFieldTestContext test = new();

    public NumberFieldIosTests()
    {
        BunitJSModuleInterop interactions = test.Context.JSInterop.SetupModule("./_content/Blazix.Primitives/js/interactions.js");
        interactions.Setup<string>("readPlatformFlags").SetResult("""{"voiceOver":false,"mac":false,"ios":true}""");
    }

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "uses the decimal keyboard when negative values are impossible")]
    [Trait("Requirement", "NUM-STA-19")]
    public void Uses_the_decimal_keyboard_when_negative_values_are_impossible()
    {
        IRenderedComponent<NumberFieldRoot> cut = Render(0);

        cut.WaitForAssertion(() => Assert.Equal("decimal", cut.Find("input[type=text]").GetAttribute("inputmode")));
    }

    [Fact(DisplayName = "uses the text keyboard when negative values are allowed")]
    [Trait("Requirement", "NUM-STA-19")]
    public void Uses_the_text_keyboard_when_negative_values_are_allowed()
    {
        IRenderedComponent<NumberFieldRoot> cut = Render(-5);

        cut.WaitForAssertion(() => Assert.Equal("text", cut.Find("input[type=text]").GetAttribute("inputmode")));
    }

    [Fact(DisplayName = "uses the text keyboard when no minimum is set")]
    [Trait("Requirement", "NUM-STA-19")]
    public void Uses_the_text_keyboard_when_no_minimum_is_set()
    {
        IRenderedComponent<NumberFieldRoot> cut = Render(null);

        cut.WaitForAssertion(() => Assert.Equal("text", cut.Find("input[type=text]").GetAttribute("inputmode")));
    }

    private IRenderedComponent<NumberFieldRoot> Render(double? min)
    {
        return test.Context.Render<NumberFieldRoot>(p => p
            .Add(x => x.Culture, CultureInfo.GetCultureInfo("en-US"))
            .Add(x => x.Min, min)
            .Add(x => x.ChildContent, (RenderFragment)(b =>
            {
                b.OpenComponent<NumberFieldInput>(0);
                b.CloseComponent();
            })));
    }
}
