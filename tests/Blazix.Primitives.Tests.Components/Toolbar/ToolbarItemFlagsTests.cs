using System.Text.Json;
using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// M3 track 1's E3 on the toolbar (TLB-STA-02, TLB-STA-03, TLB-STA-06): each item sends its disabled and
// focusableWhenDisabled flags, and the composite module derives the disabled indices from them. The navigation they cause is
// the module's, in tests/browser/toolbar.spec.ts.
/// <summary>The flags the toolbar's items send and the root's derived disabled indices.</summary>
public sealed class ToolbarItemFlagsTests : IDisposable
{
    private readonly ToolbarTestContext test = new();

    private BunitContext Context => test.Context;

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "port: the toolbar's root derives its disabled indices from the items' flags, and a standalone toggle group's does not")]
    [Trait("Requirement", "TLB-STA-02")]
    [Trait("Requirement", "TGG-KEY-08")]
    public void Port_the_toolbars_root_derives_its_disabled_indices_from_the_items_flags()
    {
        Context.Render<ToolbarRoot>(p => p.AddChildContent<ToolbarButton>());
        JsonElement toolbar = JsonDocument.Parse(Assert.Single(test.CompositeAttaches)).RootElement;
        Assert.True(toolbar.GetProperty("deriveDisabledIndices").GetBoolean());
        Assert.True(!toolbar.TryGetProperty("disabledIndices", out JsonElement indices) || indices.ValueKind == JsonValueKind.Null);

        // Toggle group §16 Q3 (settled by the orchestrator 2026-10-26): the option is per root.
        Context.Render<ToggleGroup>(p => p.AddChildContent(ToggleTestContext.Toggles("one")));
        Assert.False(JsonDocument.Parse(test.CompositeAttaches[^1]).RootElement.TryGetProperty("deriveDisabledIndices", out _));
    }

    [Fact(DisplayName = "port: a button and an input send their disabled state and FocusableWhenDisabled, and a link is never disabled and focusable")]
    [Trait("Requirement", "TLB-STA-03")]
    [Trait("Requirement", "TLB-API-07")]
    [Trait("Requirement", "TLB-API-13")]
    [Trait("Requirement", "TLB-API-11")]
    public void Port_each_item_sends_its_flags()
    {
        IRenderedComponent<ToolbarRoot> cut = Context.Render<ToolbarRoot>(p => p.AddChildContent(builder =>
        {
            builder.OpenComponent<ToolbarButton>(0);
            builder.AddComponentParameter(1, nameof(ToolbarButton.Disabled), true);
            builder.CloseComponent();
            builder.OpenComponent<ToolbarButton>(2);
            builder.AddComponentParameter(3, nameof(ToolbarButton.Disabled), true);
            builder.AddComponentParameter(4, nameof(ToolbarButton.FocusableWhenDisabled), false);
            builder.CloseComponent();
            builder.OpenComponent<ToolbarButton>(5);
            builder.AddComponentParameter(6, nameof(ToolbarButton.FocusableWhenDisabled), false);
            builder.CloseComponent();
            builder.OpenComponent<ToolbarLink>(7);
            builder.AddAttribute(8, "href", "#");
            builder.CloseComponent();
            builder.OpenComponent<ToolbarInput>(9);
            builder.AddComponentParameter(10, nameof(ToolbarInput.Disabled), true);
            builder.AddComponentParameter(11, nameof(ToolbarInput.FocusableWhenDisabled), false);
            builder.CloseComponent();
        }));

        string[] ids = [.. cut.FindAll("[tabindex='-1']").Select(item => item.GetAttribute("id")!)];
        Assert.Equal(
            [
                $$"""{"id":"{{ids[0]}}","disabled":true,"focusableWhenDisabled":true}""",
                $$"""{"id":"{{ids[1]}}","disabled":true}""",
                $$"""{"id":"{{ids[2]}}"}""",
                $$"""{"id":"{{ids[3]}}","focusableWhenDisabled":true}""",
                $$"""{"id":"{{ids[4]}}","disabled":true}""",
            ],
            ToolbarTestContext.Items(test.LastCompositeOptions));
    }

    [Fact(DisplayName = "port: a change of an item's disabled state from any source sends its new flags in one update")]
    [Trait("Requirement", "TLB-STA-06")]
    [Trait("Requirement", "TLB-STA-05")]
    public void Port_a_change_of_an_items_disabled_state_sends_its_new_flags()
    {
        IRenderedComponent<ToolbarRoot> cut = Context.Render<ToolbarRoot>(p => p.AddChildContent<ToolbarGroup>(g => g
            .AddChildContent<ToolbarInput>()));
        string id = cut.Find("input").GetAttribute("id")!;
        Assert.Equal([$$"""{"id":"{{id}}","focusableWhenDisabled":true}"""], ToolbarTestContext.Items(test.LastCompositeOptions));

        cut.Render(p => p.Add(x => x.Disabled, true));
        Assert.Single(test.CompositeUpdates);
        Assert.Equal([$$"""{"id":"{{id}}","disabled":true,"focusableWhenDisabled":true}"""], ToolbarTestContext.Items(test.LastCompositeOptions));

        cut.Render(p => p.Add(x => x.Disabled, false));
        Assert.Equal([$$"""{"id":"{{id}}","focusableWhenDisabled":true}"""], ToolbarTestContext.Items(test.LastCompositeOptions));

        cut.FindComponent<ToolbarGroup>().Render(p => p.Add(x => x.Disabled, true));
        Assert.Equal([$$"""{"id":"{{id}}","disabled":true,"focusableWhenDisabled":true}"""], ToolbarTestContext.Items(test.LastCompositeOptions));
        Assert.Equal(3, test.CompositeUpdates.Count);
    }
}
