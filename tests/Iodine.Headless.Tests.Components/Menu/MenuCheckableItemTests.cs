using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

// Family tests use the project's root namespace (tests/Blazix.Primitives.Tests.Components/CONTEXT.md).
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// L2: <c>MenuCheckboxItem.test.tsx</c>'s titles over <see cref="MenuCheckableHost"/> (MNU-API-34, API-41, API-42, STA-14,
/// DOM-16's plain title, ARIA-08, PTR-15, EVT-08). A press is the item's C# <c>onclick</c>; the keyboard titles are L4.
/// </summary>
public sealed class MenuCheckboxItemTests : BunitContext
{
    public MenuCheckboxItemTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlazixPrimitives();
    }

    [Theory(DisplayName = "adds the state and ARIA attributes when checked=$checked")]
    [InlineData(true, "true", "checked")]
    [InlineData(false, "false", "unchecked")]
    [Trait("Requirement", "MNU-API-41")]
    [Trait("Requirement", "MNU-ARIA-08")]
    [Trait("Requirement", "MNU-DOM-16")]
    public void Adds_the_state_and_aria_attributes(bool isChecked, string ariaChecked, string dataState)
    {
        IRenderedComponent<MenuCheckableHost> cut = Render<MenuCheckableHost>(p => p.Add(x => x.Checked, isChecked));

        IElement item = cut.Find("[role=menuitemcheckbox]");
        Assert.Equal(ariaChecked, item.GetAttribute("aria-checked"));
        Assert.True(item.HasAttribute($"data-{dataState}"));
    }

    [Fact(DisplayName = "toggles the checked state when clicked")]
    [Trait("Requirement", "MNU-STA-14")]
    [Trait("Requirement", "MNU-PTR-15")]
    public void Toggles_the_checked_state_when_clicked()
    {
        IRenderedComponent<MenuCheckableHost> cut = Render<MenuCheckableHost>();

        cut.Find("#item-1").Click();
        Assert.Equal(("true", true), (cut.Find("#item-1").GetAttribute("aria-checked"), cut.Find("#item-1").HasAttribute("data-checked")));

        cut.Find("#item-1").Click();
        Assert.Equal(("false", true), (cut.Find("#item-1").GetAttribute("aria-checked"), cut.Find("#item-1").HasAttribute("data-unchecked")));
    }

    [Fact(DisplayName = "calls `onCheckedChange` when the item is clicked")]
    [Trait("Requirement", "MNU-API-42")]
    [Trait("Requirement", "MNU-EVT-08")]
    public void Calls_on_checked_change_when_the_item_is_clicked()
    {
        IRenderedComponent<MenuCheckableHost> cut = Render<MenuCheckableHost>();

        cut.Find("#item-1").Click();
        Assert.Equal([true], cut.Instance.CheckedChanges);

        cut.Find("#item-1").Click();
        Assert.Equal([true, false], cut.Instance.CheckedChanges);
    }

    [Fact(DisplayName = "does not toggle when `onCheckedChange` cancels the event")]
    [Trait("Requirement", "MNU-API-42")]
    [Trait("Requirement", "MNU-EVT-08")]
    public void Does_not_toggle_when_on_checked_change_cancels()
    {
        IRenderedComponent<MenuCheckableHost> cut = Render<MenuCheckableHost>(p => p.Add(x => x.Cancel, true));

        cut.Find("#item-1").Click();

        IElement item = cut.Find("#item-1");
        Assert.Equal("false", item.GetAttribute("aria-checked"));
        Assert.False(item.HasAttribute("data-checked"));
    }

    [Fact(DisplayName = "keeps the state when closed and reopened")]
    [Trait("Requirement", "MNU-API-41")]
    [Trait("Requirement", "MNU-STA-14")]
    public void Keeps_the_state_when_closed_and_reopened()
    {
        IRenderedComponent<MenuCheckableHost> cut = Render<MenuCheckableHost>(p => p.Add(x => x.KeepMounted, true));

        cut.Find("#item-1").Click();
        cut.InvokeAsync(() => cut.Instance.SetOpen(false));
        Assert.Equal("false", cut.Find("[data-testid=trigger]").GetAttribute("aria-expanded"));
        cut.InvokeAsync(() => cut.Instance.SetOpen(true));

        IElement item = cut.Find("#item-1");
        Assert.Equal("true", item.GetAttribute("aria-checked"));
        Assert.True(item.HasAttribute("data-checked"));
    }

    [Fact(DisplayName = "when `closeOnClick=true`, closes the menu when the item is clicked")]
    [Trait("Requirement", "MNU-API-34")]
    [Trait("Requirement", "MNU-PTR-15")]
    public void Closes_the_menu_with_close_on_click()
    {
        IRenderedComponent<MenuCheckableHost> cut = Render<MenuCheckableHost>(p => p.Add(x => x.CloseOnClick, true));

        cut.Find("#item-1").Click();

        Assert.Equal(["False:ItemPress"], cut.Instance.OpenChanges);
    }

    [Fact(DisplayName = "does not close the menu when the item is clicked by default")]
    [Trait("Requirement", "MNU-API-34")]
    [Trait("Requirement", "MNU-PTR-15")]
    public void Does_not_close_the_menu_by_default()
    {
        IRenderedComponent<MenuCheckableHost> cut = Render<MenuCheckableHost>();

        cut.Find("#item-1").Click();

        Assert.Empty(cut.Instance.OpenChanges);
        Assert.Equal("true", cut.Find("[data-testid=trigger]").GetAttribute("aria-expanded"));
    }

    [Fact(DisplayName = "can be focused but not interacted with when disabled")]
    [Trait("Requirement", "MNU-API-35")]
    public void Can_be_focused_but_not_interacted_with_when_disabled()
    {
        IRenderedComponent<MenuCheckableHost> cut = Render<MenuCheckableHost>(p => p.Add(x => x.FirstDisabled, true));
        IElement item = cut.Find("#item-1");
        Assert.False(item.HasAttribute("disabled"));

        item.KeyDown(new KeyboardEventArgs { Key = "Enter" });
        item.KeyUp(new KeyboardEventArgs { Key = " " });
        item.Click();

        Assert.Equal(0, cut.Instance.Handled);
        Assert.Empty(cut.Instance.CheckedChanges);
    }
}

/// <summary>
/// L2: <c>MenuRadioItem.test.tsx</c>'s titles over <see cref="MenuCheckableHost"/> (MNU-STR-09, EDGE-03, API-34, API-35,
/// API-44..47, STA-14, DOM-16's plain title, ARIA-09, PTR-15, EVT-09). A press is the item's C# <c>onclick</c>; the keyboard
/// titles are L4.
/// </summary>
public sealed class MenuRadioItemTests : BunitContext
{
    public MenuRadioItemTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlazixPrimitives();
    }

    [Fact(DisplayName = "throws when rendered outside Menu.RadioGroup")]
    [Trait("Requirement", "MNU-STR-09")]
    [Trait("Requirement", "MNU-EDGE-03")]
    public void Throws_when_rendered_outside_a_radio_group()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Render<MenuCheckableHost>(p => p.Add(x => x.Kind, "radio-outside")));

        Assert.Equal("Blazix: The `MenuRadioGroup` context is missing. MenuRadioGroup parts must be placed within `<MenuRadioGroup>`.", error.Message);
    }

    [Fact(DisplayName = "adds the state and ARIA attributes when selected")]
    [Trait("Requirement", "MNU-API-44")]
    [Trait("Requirement", "MNU-ARIA-09")]
    [Trait("Requirement", "MNU-DOM-16")]
    public void Adds_the_state_and_aria_attributes_when_selected()
    {
        IRenderedComponent<MenuCheckableHost> cut = Render<MenuCheckableHost>(p => p.Add(x => x.Kind, "radio"));

        cut.Find("#item-1").Click();

        IElement item = cut.Find("#item-1");
        Assert.Equal("true", item.GetAttribute("aria-checked"));
        Assert.True(item.HasAttribute("data-checked"));
    }

    [Fact(DisplayName = "calls `onValueChange` when the item is clicked")]
    [Trait("Requirement", "MNU-API-45")]
    [Trait("Requirement", "MNU-API-47")]
    [Trait("Requirement", "MNU-EVT-09")]
    public void Calls_on_value_change_when_the_item_is_clicked()
    {
        IRenderedComponent<MenuCheckableHost> cut = Render<MenuCheckableHost>(p => p.Add(x => x.Kind, "radio"));

        cut.Find("#item-1").Click();

        Assert.Equal([1], cut.Instance.ValueChanges);
    }

    [Fact(DisplayName = "does not select when `onValueChange` cancels the event")]
    [Trait("Requirement", "MNU-API-45")]
    [Trait("Requirement", "MNU-EVT-09")]
    public void Does_not_select_when_on_value_change_cancels()
    {
        IRenderedComponent<MenuCheckableHost> cut = Render<MenuCheckableHost>(p => p
            .Add(x => x.Kind, "radio")
            .Add(x => x.Cancel, true));

        cut.Find("#item-1").Click();

        IElement item = cut.Find("#item-1");
        Assert.Equal("false", item.GetAttribute("aria-checked"));
        Assert.False(item.HasAttribute("data-checked"));
    }

    [Fact(DisplayName = "keeps the state when closed and reopened")]
    [Trait("Requirement", "MNU-API-44")]
    [Trait("Requirement", "MNU-STA-14")]
    public void Keeps_the_state_when_closed_and_reopened()
    {
        IRenderedComponent<MenuCheckableHost> cut = Render<MenuCheckableHost>(p => p
            .Add(x => x.Kind, "radio")
            .Add(x => x.KeepMounted, true));

        cut.Find("#item-1").Click();
        cut.InvokeAsync(() => cut.Instance.SetOpen(false));
        Assert.Equal("false", cut.Find("[data-testid=trigger]").GetAttribute("aria-expanded"));
        cut.InvokeAsync(() => cut.Instance.SetOpen(true));

        IElement item = cut.Find("#item-1");
        Assert.Equal("true", item.GetAttribute("aria-checked"));
        Assert.True(item.HasAttribute("data-checked"));
    }

    [Fact(DisplayName = "when `closeOnClick=true`, closes the menu when the item is clicked")]
    [Trait("Requirement", "MNU-API-34")]
    [Trait("Requirement", "MNU-PTR-15")]
    public void Closes_the_menu_with_close_on_click()
    {
        IRenderedComponent<MenuCheckableHost> cut = Render<MenuCheckableHost>(p => p
            .Add(x => x.Kind, "radio")
            .Add(x => x.CloseOnClick, true));

        cut.Find("#item-1").Click();

        Assert.Equal(["False:ItemPress"], cut.Instance.OpenChanges);
    }

    [Fact(DisplayName = "does not close the menu when the item is clicked by default")]
    [Trait("Requirement", "MNU-API-34")]
    [Trait("Requirement", "MNU-PTR-15")]
    public void Does_not_close_the_menu_by_default()
    {
        IRenderedComponent<MenuCheckableHost> cut = Render<MenuCheckableHost>(p => p.Add(x => x.Kind, "radio"));

        cut.Find("#item-1").Click();

        Assert.Empty(cut.Instance.OpenChanges);
        Assert.Equal("true", cut.Find("[data-testid=trigger]").GetAttribute("aria-expanded"));
    }

    [Fact(DisplayName = "can be focused but not interacted with when a radio group is disabled")]
    [Trait("Requirement", "MNU-API-46")]
    public void Can_be_focused_but_not_interacted_with_when_the_group_is_disabled()
    {
        IRenderedComponent<MenuCheckableHost> cut = Render<MenuCheckableHost>(p => p
            .Add(x => x.Kind, "radio")
            .Add(x => x.GroupDisabled, true));

        foreach (string id in new[] { "item-1", "item-2" })
        {
            IElement item = cut.Find($"#{id}");
            Assert.True(item.HasAttribute("data-disabled"));
            Press(item);
        }

        Assert.Equal(0, cut.Instance.Handled);
        Assert.Empty(cut.Instance.ValueChanges);
    }

    [Fact(DisplayName = "can be focused but not interacted with when individual items are disabled")]
    [Trait("Requirement", "MNU-API-35")]
    public void Can_be_focused_but_not_interacted_with_when_an_item_is_disabled()
    {
        IRenderedComponent<MenuCheckableHost> cut = Render<MenuCheckableHost>(p => p
            .Add(x => x.Kind, "radio")
            .Add(x => x.FirstDisabled, true));

        Assert.True(cut.Find("#item-1").HasAttribute("data-disabled"));
        Assert.False(cut.Find("#item-2").HasAttribute("data-disabled"));
        Press(cut.Find("#item-1"));

        Assert.Equal(0, cut.Instance.Handled);
        Assert.Empty(cut.Instance.ValueChanges);
    }

    private static void Press(IElement item)
    {
        item.KeyDown(new KeyboardEventArgs { Key = "Enter" });
        item.KeyUp(new KeyboardEventArgs { Key = " " });
        item.Click();
    }
}
