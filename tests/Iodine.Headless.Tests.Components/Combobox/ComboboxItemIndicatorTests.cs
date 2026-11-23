using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// CMB §3.25 at L2 (ComboboxItemIndicator.test.tsx): the indicator follows its item's selection, mounted only while selected
// unless kept mounted. The exit's completion is the `transitions` module's, so the unmount after a deselection is L4.
/// <summary>Combobox's ItemIndicator at L2.</summary>
public sealed class ComboboxItemIndicatorTests : IDisposable
{
    private static readonly string[] s_items = ["a", "b"];

    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "updates a mounted indicator when its item becomes unselected")]
    [Trait("Requirement", "CMB-STA-14")]
    [Trait("Requirement", "CMB-DOM-10")]
    public async Task Updates_a_mounted_indicator_when_its_item_becomes_unselected()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["DefaultOpen"] = true, ["DefaultValue"] = "a" }, keepMounted: true);
        Assert.True(Indicator(cut, "a").HasAttribute("data-selected"));

        await cut.InvokeAsync(() => cut.Find("[data-testid=item-b]").Click());

        Assert.False(Indicator(cut, "a").HasAttribute("data-selected"));
        Assert.True(Indicator(cut, "b").HasAttribute("data-selected"));
    }

    [Fact(DisplayName = "mounts only while selected when keepMounted is false")]
    [Trait("Requirement", "CMB-STA-14")]
    [Trait("Requirement", "CMB-DOM-10")]
    public void Mounts_only_while_selected_when_keepMounted_is_false()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["DefaultOpen"] = true, ["Value"] = "b" }, keepMounted: false);
        Assert.Empty(cut.FindAll("[data-testid=indicator-a]"));

        cut.Render(p => p.Add(x => x.RootParameters, new Dictionary<string, object?> { ["DefaultOpen"] = true, ["Value"] = "a" }));

        IElement indicator = Indicator(cut, "a");
        Assert.Equal("span", indicator.TagName.ToLowerInvariant());
        Assert.True(indicator.HasAttribute("data-selected"));
        Assert.Equal("true", indicator.GetAttribute("aria-hidden"));
        Assert.False(indicator.HasAttribute("selected"));
        Assert.Equal("✔️", indicator.TextContent);
    }

    [Fact(DisplayName = "preserves the kept-mounted element across selection changes")]
    [Trait("Requirement", "CMB-STA-14")]
    public void Preserves_the_kept_mounted_element_across_selection_changes()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["DefaultOpen"] = true, ["Value"] = "b" }, keepMounted: true);
        Assert.False(Indicator(cut, "a").HasAttribute("data-selected"));

        cut.Render(p => p.Add(x => x.RootParameters, new Dictionary<string, object?> { ["DefaultOpen"] = true, ["Value"] = "a" }));
        Assert.True(Indicator(cut, "a").HasAttribute("data-selected"));

        cut.Render(p => p.Add(x => x.RootParameters, new Dictionary<string, object?> { ["DefaultOpen"] = true, ["Value"] = "b" }));
        Assert.False(Indicator(cut, "a").HasAttribute("data-selected"));
    }

    [Fact(DisplayName = "port: an indicator outside an item throws the missing item error")]
    [Trait("Requirement", "CMB-STR-02")]
    public void An_indicator_outside_an_item_throws()
    {
        RenderFragment extra = builder =>
        {
            builder.OpenComponent<ComboboxItemIndicator>(0);
            builder.CloseComponent();
        };

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => test.Context.Render<ComboboxHarness>(p => p.Add(x => x.Extra, extra)));

        Assert.StartsWith("Blazix: `ComboboxItemContext` is missing.", error.Message, StringComparison.Ordinal);
    }

    private static IElement Indicator(IRenderedComponent<ComboboxHarness> cut, string item) => cut.Find($"[data-testid=indicator-{item}]");

    private IRenderedComponent<ComboboxHarness> Render(Dictionary<string, object?> root, bool keepMounted)
    {
        RenderFragment<string> indicator = item => builder =>
        {
            builder.OpenComponent<ComboboxItemIndicator>(0);
            builder.AddComponentParameter(1, "data-testid", "indicator-" + item);
            builder.AddComponentParameter(2, "KeepMounted", keepMounted);
            builder.CloseComponent();
        };

        return test.Context.Render<ComboboxHarness>(p => p
            .Add(x => x.RootParameters, root)
            .Add(x => x.StaticItems, s_items)
            .Add(x => x.ItemExtra, indicator));
    }
}
