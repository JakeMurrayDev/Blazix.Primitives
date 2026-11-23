using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

// Family tests use the project's root namespace (tests/Blazix.Primitives.Tests.Components/CONTEXT.md).
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// L2: render titles of <c>MenuTrigger.test.tsx</c>, <c>MenuPositioner.test.tsx</c>, <c>MenuSubmenuTrigger.test.tsx</c> and
/// <c>MenuRoot.test.tsx</c> over <see cref="MenuPartsHost"/> and <see cref="MenuTreeHost"/>: the parts' errors, the
/// disabled triggers, the trigger's role inside a Popover, the popup's orientation and the portals' owners.
/// </summary>
public sealed class MenuPartsRenderTests : BunitContext
{
    public MenuPartsRenderTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlazixPrimitives();
    }

    [Fact(DisplayName = "throws without Menu.Root or a handle")]
    [Trait("Requirement", "MNU-STR-02")]
    [Trait("Requirement", "MNU-EDGE-01")]
    public void Trigger_throws_without_a_root_or_a_handle()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Render<MenuPartsHost>(p => p.Add(x => x.Case, "trigger-alone")));

        Assert.Equal("Blazix: `<MenuTrigger>` must be either used within a `<MenuRoot>` component or provided with a handle.", error.Message);
    }

    [Fact(DisplayName = "throws when rendered outside Menu.Portal")]
    [Trait("Requirement", "MNU-STR-04")]
    [Trait("Requirement", "MNU-EDGE-03")]
    public void Positioner_throws_when_rendered_outside_the_portal()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Render<MenuPartsHost>(p => p.Add(x => x.Case, "positioner-outside-portal")));

        Assert.Equal("Blazix: `<MenuPortal>` is missing.", error.Message);
    }

    [Fact(DisplayName = "should render a disabled button")]
    [Trait("Requirement", "MNU-API-16")]
    public void Trigger_renders_a_disabled_button()
    {
        IRenderedComponent<MenuPartsHost> cut = Render<MenuPartsHost>(p => p
            .Add(x => x.Case, "trigger-disabled")
            .Add(x => x.TriggerDisabled, true));

        Assert.True(cut.Find("button").HasAttribute("disabled"));
    }

    [Fact(DisplayName = "does not have role prop inside a Popover")]
    [Trait("Requirement", "MNU-ARIA-17")]
    public void Trigger_has_no_role_inside_a_popover()
    {
        IRenderedComponent<MenuPartsHost> cut = Render<MenuPartsHost>(p => p.Add(x => x.Case, "trigger-in-popover"));

        Assert.False(cut.Find("[data-testid=menu-trigger]").HasAttribute("role"));
    }

    [Fact(DisplayName = "has a role prop inside a Popover when not a native button")]
    [Trait("Requirement", "MNU-ARIA-17")]
    public void Trigger_has_the_button_role_inside_a_popover_when_not_native()
    {
        IRenderedComponent<MenuPartsHost> cut = Render<MenuPartsHost>(p => p
            .Add(x => x.Case, "trigger-in-popover")
            .Add(x => x.NotNative, true));

        Assert.Equal("button", cut.Find("[data-testid=menu-trigger]").GetAttribute("role"));
    }

    [Fact(DisplayName = "throws when rendered outside Menu.SubmenuRoot")]
    [Trait("Requirement", "MNU-STR-03")]
    [Trait("Requirement", "MNU-EDGE-03")]
    public void Submenu_trigger_throws_when_rendered_outside_the_submenu_root()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Render<MenuPartsHost>(p => p.Add(x => x.Case, "submenu-trigger-alone")));

        Assert.Equal("Blazix: `<MenuSubmenuTrigger>` must be placed in `<MenuSubmenuRoot>`.", error.Message);
    }

    [Fact(DisplayName = "throws when Menu.SubmenuRoot is rendered outside a menu")]
    [Trait("Requirement", "MNU-STR-07")]
    [Trait("Requirement", "MNU-EDGE-03")]
    public void Submenu_root_throws_when_rendered_outside_a_menu()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Render<MenuPartsHost>(p => p.Add(x => x.Case, "submenu-root-alone")));

        Assert.StartsWith("Blazix: The `MenuRoot` context is missing.", error.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "should render with disabled attributes when disabled prop is set")]
    [Trait("Requirement", "MNU-DOM-18")]
    [Trait("Requirement", "MNU-API-52")]
    public void Submenu_trigger_renders_the_disabled_attributes()
    {
        IRenderedComponent<MenuPartsHost> cut = Render<MenuPartsHost>(p => p.Add(x => x.SubmenuDisabled, true));

        IElement trigger = cut.Find("[data-testid=submenu-trigger]");
        Assert.True(trigger.HasAttribute("data-disabled"));
        Assert.Equal("true", trigger.GetAttribute("aria-disabled"));
    }

    [Fact(DisplayName = "sets aria-orientation on a horizontal popup")]
    [Trait("Requirement", "MNU-API-09")]
    [Trait("Requirement", "MNU-ARIA-04")]
    public void Popup_sets_aria_orientation_when_horizontal()
    {
        IRenderedComponent<MenuTreeHost> cut = Render<MenuTreeHost>(p => p.Add(x => x.Orientation, Blazix.Primitives.Orientation.Horizontal));

        Assert.Equal("horizontal", cut.Find("[role=menu]").GetAttribute("aria-orientation"));
    }

    [Fact(DisplayName = "does not render aria-orientation on a vertical popup")]
    [Trait("Requirement", "MNU-ARIA-04")]
    public void Popup_has_no_aria_orientation_when_vertical()
    {
        IRenderedComponent<MenuTreeHost> cut = Render<MenuTreeHost>();

        Assert.False(cut.Find("[role=menu]").HasAttribute("aria-orientation"));
    }

    [Fact(DisplayName = "renders root menu portal ownership without an accessibility role")]
    [Trait("Requirement", "MNU-STR-18")]
    [Trait("Requirement", "MNU-DOM-26")]
    public void Root_portal_owner_has_no_role()
    {
        IRenderedComponent<MenuPartsHost> cut = Render<MenuPartsHost>();

        IElement portal = cut.Find("[data-testid=menu]").Closest("[data-base-ui-portal]")!;
        Assert.False(string.IsNullOrEmpty(portal.Id));
        IElement owner = cut.FindAll("span[aria-owns]").Single(element => element.GetAttribute("aria-owns") == portal.Id);
        Assert.False(owner.HasAttribute("role"));
    }

    [Fact(DisplayName = "renders submenu portal ownership as an allowed menu child")]
    [Trait("Requirement", "MNU-STR-18")]
    [Trait("Requirement", "MNU-ARIA-18")]
    [Trait("Requirement", "MNU-DOM-26")]
    public void Submenu_portal_owner_is_a_group_in_the_menu()
    {
        IRenderedComponent<MenuPartsHost> cut = Render<MenuPartsHost>(p => p.Add(x => x.SubmenuOpen, true));

        IElement portal = cut.Find("[data-testid=submenu]").Closest("[data-base-ui-portal]")!;
        Assert.False(string.IsNullOrEmpty(portal.Id));
        IElement owner = cut.Find("[data-testid=menu]").QuerySelectorAll("span[aria-owns]").Single();
        Assert.Equal("group", owner.GetAttribute("role"));
        Assert.Equal(portal.Id, owner.GetAttribute("aria-owns"));
        Assert.False(cut.Find("[data-testid=submenu-trigger]").HasAttribute("aria-owns"));
    }

    [Fact(DisplayName = "disables submenu triggers when controlled open")]
    [Trait("Requirement", "MNU-API-52")]
    public void Root_disabled_disables_the_submenu_trigger()
    {
        IRenderedComponent<MenuPartsHost> cut = Render<MenuPartsHost>(p => p.Add(x => x.RootDisabled, true));

        IElement trigger = cut.Find("[data-testid=submenu-trigger]");
        Assert.True(trigger.HasAttribute("data-disabled"));
        trigger.Click();
        Assert.Empty(cut.FindAll("[data-testid=submenu]"));
    }

    [Theory(DisplayName = "prop: modal")]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Requirement", "MNU-API-08")]
    [Trait("Requirement", "MNU-STR-17")]
    public void Modal_renders_the_internal_backdrop_only_when_true(bool modal)
    {
        // "should render an internal backdrop when `true`" and "should not render an internal backdrop when `false`".
        IRenderedComponent<MenuTreeHost> cut = Render<MenuTreeHost>(p => p
            .Add(x => x.Modal, modal)
            .Add(x => x.PositionerAttributes, new Dictionary<string, object> { ["data-testid"] = "positioner" }));

        IElement? previous = cut.Find("[data-testid=positioner]").PreviousElementSibling;
        Assert.Equal(modal, previous?.GetAttribute("role") == "presentation");
    }

    [Theory(DisplayName = "port: the trigger renders data-disabled iff its effective disabled state")]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [Trait("Requirement", "MNU-DOM-02")]
    public void Trigger_renders_data_disabled_iff_effectively_disabled(bool trigger, bool root, bool expected)
    {
        IRenderedComponent<MenuPartsHost> cut = Render<MenuPartsHost>(p => p
            .Add(x => x.Case, "trigger-disabled")
            .Add(x => x.TriggerDisabled, trigger)
            .Add(x => x.RootDisabled, root));

        Assert.Equal(expected, cut.Find("button").HasAttribute("data-disabled"));
    }

    [Theory(DisplayName = "port: a radio group renders data-disabled when it is disabled")]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Requirement", "MNU-DOM-29")]
    public void Radio_group_renders_data_disabled_when_disabled(bool disabled)
    {
        IRenderedComponent<MenuPartsHost> cut = Render<MenuPartsHost>(p => p
            .Add(x => x.Case, "radio-group")
            .Add(x => x.RadioGroupDisabled, disabled));

        Assert.Equal(disabled, cut.Find("[role=group]").HasAttribute("data-disabled"));
    }

    [Theory(DisplayName = "port: collision avoidance defaults to no axis fallback at top level and the end side in a submenu")]
    [InlineData(false, "none")]
    [InlineData(true, "end")]
    [Trait("Requirement", "MNU-API-29")]
    public void Collision_avoidance_defaults_by_parent_kind(bool submenu, string fallbackAxisSide)
    {
        BunitJSModuleInterop positioning = JSInterop.SetupModule("./_content/Blazix.Primitives/js/positioning.js");
        positioning.SetupModule("attach", _ => true);

        Render<MenuPartsHost>(p => p.Add(x => x.SubmenuOpen, submenu));

        string[] options = positioning.Invocations["attach"].Select(attach => (string)attach.Arguments[1]!).ToArray();
        Assert.Contains(options, json => json.Contains($"\"fallbackAxisSide\":\"{fallbackAxisSide}\"", StringComparison.Ordinal));
    }

    // MenuGroup.test.tsx and MenuRadioGroup.test.tsx share the title; the group renders inside an open menu, as both parts
    // read the menu's context here.
    [Theory(DisplayName = "renders a div with the `group` role")]
    [InlineData("group")]
    [InlineData("radio-group")]
    [Trait("Requirement", "MNU-ARIA-10")]
    public void Group_renders_a_div_with_the_group_role(string part)
    {
        IRenderedComponent<MenuPartsHost> cut = Render<MenuPartsHost>(p => p.Add(x => x.Case, part));

        IElement group = cut.Find("[role=group]");
        Assert.Equal("DIV", group.TagName);
    }

    [Fact(DisplayName = "leaves lazy flipping off for a plain menu")]
    [Trait("Requirement", "MNU-POS-05")]
    public void Positioner_leaves_lazy_flipping_off_for_a_plain_menu()
    {
        BunitJSModuleInterop positioning = JSInterop.SetupModule("./_content/Blazix.Primitives/js/positioning.js");
        positioning.SetupModule("attach", _ => true);

        Render<MenuPartsHost>();

        string[] options = positioning.Invocations["attach"].Select(attach => (string)attach.Arguments[1]!).ToArray();
        Assert.NotEmpty(options);
        Assert.All(options, json => Assert.DoesNotContain("lazyFlip", json, StringComparison.Ordinal));
    }

    [Fact(DisplayName = "port: an open submenu's trigger renders tabindex 0, and the list's roving value once it closes")]
    [Trait("Requirement", "MNU-KEY-15")]
    [Trait("Requirement", "MNU-DOM-18")]
    public void Open_submenu_trigger_renders_tabindex_zero()
    {
        IRenderedComponent<MenuPartsHost> cut = Render<MenuPartsHost>(p => p.Add(x => x.SubmenuOpen, true));
        Assert.Equal("0", cut.Find("[data-testid=submenu-trigger]").GetAttribute("tabindex"));

        cut.Render(p => p.Add(x => x.SubmenuOpen, false));

        cut.WaitForAssertion(() => Assert.Equal("-1", cut.Find("[data-testid=submenu-trigger]").GetAttribute("tabindex")));
    }

    [Fact(DisplayName = "closes an open submenu with a sibling reason when its controlled parent closes")]
    [Trait("Requirement", "MNU-DIS-04")]
    [Trait("Requirement", "MNU-API-49")]
    public void Controlled_parent_close_closes_the_submenu_with_a_sibling_reason()
    {
        IRenderedComponent<MenuControlledParentHost> cut = Render<MenuControlledParentHost>();

        cut.Render(p => p.Add(x => x.Open, false));

        cut.WaitForAssertion(() => Assert.Equal(["False:SiblingOpen"], cut.Instance.SubmenuChanges));
    }
}
