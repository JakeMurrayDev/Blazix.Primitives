using Bunit;
using Microsoft.Extensions.DependencyInjection;

// Family tests use the project's root namespace (tests/Blazix.Primitives.Tests.Components/CONTEXT.md).
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// L2: the Menu paths a bar reads (design points 2 and 3 of M5 track 8, settled by the orchestrator 2026-11-11), on a
/// recording bar parent. A bar menu open at its first render is heard (MBR-STA-02, STA-04; Base UI's Positioner reports on
/// mount, MenuPositioner.tsx:218-227); a trigger in the bar holding a detached Root's popup makes the bar its store's parent
/// (MBR-STR-04; MenuTrigger.tsx:95-108); a bar menu with no Positioner is not heard (MBR-STA-04's Positioner clause).
/// </summary>
public sealed class MenubarMenuGapTests : BunitContext
{
    public MenubarMenuGapTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlazixPrimitives();
    }

    [Fact(DisplayName = "port: a bar menu open at its first render is heard by the bar")]
    [Trait("Requirement", "MBR-STA-02")]
    [Trait("Requirement", "MBR-STA-04")]
    public void A_bar_menu_open_at_its_first_render_is_heard()
    {
        IRenderedComponent<MenubarMenuGapHost> cut = Render<MenubarMenuGapHost>();

        cut.WaitForAssertion(() => Assert.Contains(cut.Instance.Parent.Changes, change => change.Open), TimeSpan.FromSeconds(2));
    }

    [Fact(DisplayName = "port: a detached menu whose bar trigger holds the popup takes the bar as its parent")]
    [Trait("Requirement", "MBR-STR-04")]
    public void A_detached_menu_held_by_a_bar_trigger_takes_the_bar_as_its_parent()
    {
        IRenderedComponent<MenubarMenuGapHost> cut = Render<MenubarMenuGapHost>(p => p.Add(x => x.Case, "detached"));

        Assert.Equal(MenubarRecordingParent.BarRootId, cut.Find("[data-testid=\"popup\"]").GetAttribute("data-rootownerid"));
        Assert.Contains(cut.Instance.Parent.Changes, change => change.Open);
    }

    // MenuRoot.tsx:193, 516-568: the Root's list reads the store's parent, so a detached menu held by a bar trigger
    // navigates as the bar's nested list (the contained case is the control).
    [Theory(DisplayName = "port: a bar menu navigates as a nested list of the bar, detached or contained")]
    [InlineData("initial-open")]
    [InlineData("detached")]
    [Trait("Requirement", "MBR-STR-04")]
    public void A_bar_menu_navigates_as_a_nested_list_of_the_bar(string setup)
    {
        IRenderedComponent<MenubarMenuGapHost> cut = Render<MenubarMenuGapHost>(p => p.Add(x => x.Case, setup));

        cut.WaitForAssertion(
            () => Assert.Contains(
                JSInterop.Invocations.SelectMany(invocation => invocation.Arguments.OfType<string>()),
                argument => argument.Contains("\"nested\":true", StringComparison.Ordinal) && argument.Contains("\"parentOrientation\":\"horizontal\"", StringComparison.Ordinal)),
            TimeSpan.FromSeconds(2));
    }

    // Design point 1 (a): Base UI arms on every closed trigger's press and resets the arm for a menu that is not open only
    // when the trigger has no parent (MenuTrigger.tsx:120-124, 240-256).
    [Fact(DisplayName = "port: a bar trigger arms the release and keeps the arm when its menu closes")]
    [Trait("Requirement", "MBR-STA-07")]
    public void A_bar_trigger_arms_the_release_and_keeps_the_arm()
    {
        Render<MenubarMenuGapHost>();

        Assert.Contains(
            JSInterop.Invocations.SelectMany(invocation => invocation.Arguments.OfType<string>()),
            argument => argument.Contains("\"release\":true", StringComparison.Ordinal) && argument.Contains("\"keepArmedWhenClosed\":true", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "port: a bar menu with no Positioner is not heard by the bar")]
    [Trait("Requirement", "MBR-STA-04")]
    public void A_bar_menu_with_no_positioner_is_not_heard()
    {
        IRenderedComponent<MenubarMenuGapHost> cut = Render<MenubarMenuGapHost>(p => p.Add(x => x.Case, "no-positioner"));

        cut.Render(p => p.Add(x => x.Open, true));

        Assert.DoesNotContain(cut.Instance.Parent.Changes, change => change.Open);
    }
}
