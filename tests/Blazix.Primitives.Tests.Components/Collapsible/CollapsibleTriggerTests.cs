using Bunit;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The <c>[L2]</c> rows of docs/components/collapsible.md that <c>CollapsibleTrigger.test.tsx</c> covers: the missing-root
/// error (COL-STR-02, COL-EDGE-01), the disabled matrix of the button mechanism (COL-DOM-07/08), the forwarded id
/// (COL-API-07), and the trigger without a panel (COL-STR-04). Titles are Base UI's; focus is the same title at L4.
/// </summary>
public sealed class CollapsibleTriggerTests : IDisposable
{
    private readonly CollapsibleTestContext host = new();

    private BunitContext Context => host.Context;

    /// <inheritdoc />
    public void Dispose() => host.Dispose();

    [Fact(DisplayName = "throws when rendered outside a Collapsible.Root")]
    [Trait("Requirement", "COL-STR-02")]
    [Trait("Requirement", "COL-EDGE-01")]
    public void Throws_when_rendered_outside_a_Collapsible_Root()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Context.Render<CollapsibleTrigger>());

        Assert.Equal("Blazix: The `CollapsibleRoot` context is missing. Collapsible parts must be placed within `<CollapsibleRoot>`.", error.Message);
    }

    [Fact(DisplayName = "throws when a panel is rendered outside a Collapsible.Root")]
    [Trait("Requirement", "COL-STR-03")]
    [Trait("Requirement", "COL-EDGE-01")]
    public void Throws_when_a_panel_is_rendered_outside_a_Collapsible_Root()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Context.Render<CollapsiblePanel>());

        Assert.Equal("Blazix: The `CollapsibleRoot` context is missing. Collapsible parts must be placed within `<CollapsibleRoot>`.", error.Message);
    }

    [Fact(DisplayName = "renders the disabled attribute when disabled")]
    [Trait("Requirement", "COL-DOM-08")]
    [Trait("Requirement", "COL-STR-04")]
    public void Renders_the_disabled_attribute_when_disabled()
    {
        IRenderedComponent<CollapsibleRoot> cut = Context.Render<CollapsibleRoot>(p => p
            .Add(x => x.Disabled, true)
            .AddChildContent<CollapsibleTrigger>(trigger => trigger.AddChildContent("Trigger")));

        Assert.True(cut.Find("button").HasAttribute("disabled"));
        Assert.False(cut.Find("button").HasAttribute("aria-disabled"));
    }

    [Fact(DisplayName = "an open trigger without a panel references the default panel id")]
    [Trait("Requirement", "COL-STR-04")]
    public void An_open_trigger_without_a_panel_references_the_default_panel_id()
    {
        // §16 Q4: kept as Base UI renders it, pointing at no element.
        IRenderedComponent<CollapsibleRoot> cut = Context.Render<CollapsibleRoot>(p => p
            .AddChildContent<CollapsibleTrigger>(trigger => trigger.AddChildContent("Trigger")));

        cut.Find("button").Click();

        Assert.Equal("true", cut.Find("button").GetAttribute("aria-expanded"));
        Assert.StartsWith("base-ui-", cut.Find("button").GetAttribute("aria-controls"), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "removes a disabled non-native trigger from the tab order")]
    [Trait("Requirement", "COL-DOM-07")]
    [Trait("Requirement", "COL-DOM-08")]
    [Trait("Requirement", "COL-KEY-03")]
    public void Removes_a_disabled_non_native_trigger_from_the_tab_order()
    {
        IRenderedComponent<CollapsibleRoot> cut = Context.Render<CollapsibleRoot>(p => p
            .Add(x => x.Disabled, true)
            .AddChildContent<CollapsibleTrigger>(trigger => trigger
                .Add(x => x.NativeButton, false)
                .Add(x => x.Render, (RenderFragment)(builder =>
                {
                    builder.OpenElement(0, "span");
                    builder.CloseElement();
                }))
                .AddChildContent("Trigger")));

        AngleSharp.Dom.IElement trigger = cut.Find("[role=button]");
        Assert.Equal("SPAN", trigger.TagName);
        Assert.Equal("true", trigger.GetAttribute("aria-disabled"));
        Assert.Equal("-1", trigger.GetAttribute("tabindex"));
        Assert.False(trigger.HasAttribute("disabled"));
    }

    [Fact(DisplayName = "forwards the id prop")]
    [Trait("Requirement", "COL-API-07")]
    public void Forwards_the_id_prop()
    {
        IRenderedComponent<CollapsibleRoot> cut = Context.Render<CollapsibleRoot>(p => p
            .AddChildContent<CollapsibleTrigger>(trigger => trigger.AddUnmatched("id", "custom-trigger-id").AddChildContent("Trigger")));

        Assert.Equal("custom-trigger-id", cut.Find("button").GetAttribute("id"));
    }

    [Fact(DisplayName = "a trigger's own Disabled overrides the root's")]
    [Trait("Requirement", "COL-API-06")]
    [Trait("Requirement", "COL-DOM-05")]
    public void A_triggers_own_Disabled_overrides_the_roots()
    {
        IRenderedComponent<CollapsibleRoot> cut = Context.Render<CollapsibleRoot>(p => p
            .Add(x => x.Disabled, true)
            .AddChildContent<CollapsibleTrigger>(trigger => trigger.Add(x => x.Disabled, false).AddUnmatched("data-testid", "enabled"))
            .AddChildContent<CollapsibleTrigger>(trigger => trigger.AddUnmatched("data-testid", "inherits")));

        Assert.False(cut.Find("[data-testid=enabled]").HasAttribute("disabled"));
        Assert.False(cut.Find("[data-testid=enabled]").HasAttribute("data-disabled"));
        Assert.True(cut.Find("[data-testid=inherits]").HasAttribute("disabled"));

        cut.Find("[data-testid=enabled]").Click();
        Assert.Equal("true", cut.Find("[data-testid=enabled]").GetAttribute("aria-expanded"));
    }
}
