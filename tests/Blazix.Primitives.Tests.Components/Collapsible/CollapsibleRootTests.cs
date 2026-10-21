using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The <c>[L2]</c> rows of docs/components/collapsible.md that <c>CollapsibleRoot.test.tsx</c> covers: the open state as a
/// request (COL-API-01..04, COL-STA-01..03, COL-EVT-01..03/06/07), the attributes and ARIA C# renders (COL-DOM-01/04/05/09/10/16,
/// COL-ARIA-01..04) and the panel id registration (COL-STA-09, COL-EDGE-09). Titles are Base UI's. Visibility, focus and
/// keys are the same titles at L4 (tests/browser/collapsible.spec.ts); here the end of a close is the module's completion
/// message, driven into the panel's twin as the module would send it.
/// </summary>
public sealed class CollapsibleRootTests : IDisposable
{
    private const string PanelContent = "This is panel content";

    private readonly CollapsibleTestContext host = new();

    private BunitContext Context => host.Context;

    /// <inheritdoc />
    public void Dispose() => host.Dispose();

    [Fact(DisplayName = "sets ARIA attributes")]
    [Trait("Requirement", "COL-ARIA-01")]
    [Trait("Requirement", "COL-ARIA-02")]
    [Trait("Requirement", "COL-DOM-10")]
    public void Sets_ARIA_attributes()
    {
        IRenderedComponent<CollapsibleRoot> cut = Context.Render<CollapsibleRoot>(p => p
            .Add(x => x.DefaultOpen, true)
            .AddChildContent<CollapsibleTrigger>()
            .AddChildContent<CollapsiblePanel>(panel => panel.AddUnmatched("data-testid", "panel")));

        IElement trigger = cut.Find("button");
        IElement panelElement = cut.Find("[data-testid=panel]");
        Assert.True(trigger.HasAttribute("aria-expanded"));
        Assert.True(trigger.HasAttribute("aria-controls"));
        Assert.Equal(panelElement.GetAttribute("id"), trigger.GetAttribute("aria-controls"));
    }

    [Fact(DisplayName = "references manual panel id in trigger aria-controls")]
    [Trait("Requirement", "COL-API-10")]
    [Trait("Requirement", "COL-ARIA-03")]
    [Trait("Requirement", "COL-DOM-10")]
    public void References_manual_panel_id_in_trigger_aria_controls()
    {
        IRenderedComponent<CollapsibleRoot> cut = Context.Render<CollapsibleRoot>(p => p
            .Add(x => x.DefaultOpen, true)
            .AddChildContent<CollapsibleTrigger>()
            .AddChildContent<CollapsiblePanel>(panel => panel.AddUnmatched("id", "custom-panel-id").AddUnmatched("data-testid", "panel")));

        Assert.Equal("custom-panel-id", cut.Find("button").GetAttribute("aria-controls"));
        Assert.Equal("custom-panel-id", cut.Find("[data-testid=panel]").GetAttribute("id"));
    }

    [Fact(DisplayName = "unregisters and restores the generated panel id when the panel remounts")]
    [Trait("Requirement", "COL-STA-09")]
    [Trait("Requirement", "COL-ARIA-04")]
    [Trait("Requirement", "COL-EDGE-09")]
    public void Unregisters_and_restores_the_generated_panel_id_when_the_panel_remounts()
    {
        IRenderedComponent<CollapsibleConsumers> cut = Context.Render<CollapsibleConsumers>(p => p
            .Add(x => x.Mode, CollapsibleConsumers.ConsumerMode.Remount)
            .Add(x => x.PanelMounted, true));
        Assert.True(cut.Find("button").HasAttribute("aria-controls"));

        cut.Render(p => p.Add(x => x.Mode, CollapsibleConsumers.ConsumerMode.Remount).Add(x => x.PanelMounted, false));
        Assert.False(cut.Find("button").HasAttribute("aria-controls"));

        cut.Render(p => p.Add(x => x.Mode, CollapsibleConsumers.ConsumerMode.Remount).Add(x => x.PanelMounted, true));
        Assert.Equal(cut.Find("[data-testid=panel]").GetAttribute("id"), cut.Find("button").GetAttribute("aria-controls"));
    }

    [Fact(DisplayName = "disabled status")]
    [Trait("Requirement", "COL-API-04")]
    [Trait("Requirement", "COL-DOM-05")]
    public void Disabled_status()
    {
        IRenderedComponent<CollapsibleRoot> cut = Context.Render<CollapsibleRoot>(p => p
            .Add(x => x.Disabled, true)
            .AddChildContent<CollapsibleTrigger>()
            .AddChildContent<CollapsiblePanel>(panel => panel.AddUnmatched("data-testid", "panel").Add(x => x.KeepMounted, true)));

        Assert.True(cut.Find("button").HasAttribute("data-disabled"));
        // COL-API-04: disabled is in every part's state (COL-DOM-02, COL-DOM-19).
        Assert.True(cut.Find("div").HasAttribute("data-disabled"));
        Assert.True(cut.Find("[data-testid=panel]").HasAttribute("data-disabled"));
    }

    [Fact(DisplayName = "does not toggle or call onOpenChange when clicked while disabled")]
    [Trait("Requirement", "COL-PTR-02")]
    public void Does_not_toggle_or_call_onOpenChange_when_clicked_while_disabled()
    {
        int calls = 0;
        IRenderedComponent<CollapsibleRoot> cut = Context.Render<CollapsibleRoot>(p => p
            .Add(x => x.Disabled, true)
            .Add(x => x.OnOpenChange, (ValueChangeEventArgs<bool> _) => calls++)
            .AddChildContent<CollapsibleTrigger>(trigger => trigger.AddChildContent("Trigger"))
            .AddChildContent<CollapsiblePanel>(panel => panel.AddChildContent(PanelContent)));

        cut.Find("button").Click();

        Assert.Equal(0, calls);
        Assert.Equal("false", cut.Find("button").GetAttribute("aria-expanded"));
        Assert.DoesNotContain(PanelContent, cut.Markup, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "calls onOpenChange with eventDetails")]
    [Trait("Requirement", "COL-EVT-01")]
    [Trait("Requirement", "COL-EVT-02")]
    [Trait("Requirement", "COL-EVT-07")]
    [Trait("Requirement", "COL-PTR-01")]
    public void Calls_onOpenChange_with_eventDetails()
    {
        List<ValueChangeEventArgs<bool>> calls = [];
        IRenderedComponent<CollapsibleRoot> cut = Context.Render<CollapsibleRoot>(p => p
            .Add(x => x.OnOpenChange, (ValueChangeEventArgs<bool> args) => calls.Add(args))
            .AddChildContent<CollapsibleTrigger>(trigger => trigger.AddChildContent("Toggle"))
            .AddChildContent<CollapsiblePanel>(panel => panel.AddChildContent(PanelContent)));

        cut.Find("button").Click(new MouseEventArgs { Type = "click", Detail = 1 });

        ValueChangeEventArgs<bool> call = Assert.Single(calls);
        Assert.True(call.Value);
        Assert.NotNull(call.Details);
        Assert.Equal(EventReason.TriggerPress, call.Details.Reason);
        Assert.True(call.Details.Event.IsInstanceOf(EventKind.MouseEvent));
        Assert.Equal("click", call.Details.Event.Type);
        Assert.False(call.Details.IsCanceled);
        // COL-EVT-07: no trigger element is recorded, and AllowPropagation has nothing to allow.
        Assert.Null(call.Details.Trigger);
        call.Details.AllowPropagation();
        Assert.Equal("true", cut.Find("button").GetAttribute("aria-expanded"));
    }

    [Fact(DisplayName = "eventDetails.cancel() prevents opening while uncontrolled")]
    [Trait("Requirement", "COL-EVT-03")]
    [Trait("Requirement", "COL-STA-02")]
    public void EventDetails_cancel_prevents_opening_while_uncontrolled()
    {
        int calls = 0;
        IRenderedComponent<CollapsibleRoot> cut = Context.Render<CollapsibleRoot>(p => p
            .Add(x => x.OnOpenChange, (ValueChangeEventArgs<bool> args) =>
            {
                calls++;
                args.Details.Cancel();
            })
            .AddChildContent<CollapsibleTrigger>(trigger => trigger.AddChildContent("Toggle"))
            .AddChildContent<CollapsiblePanel>(panel => panel.AddChildContent(PanelContent)));

        cut.Find("button").Click();

        Assert.Equal(1, calls);
        Assert.Equal("false", cut.Find("button").GetAttribute("aria-expanded"));
        Assert.DoesNotContain(PanelContent, cut.Markup, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "eventDetails.cancel() prevents closing while uncontrolled")]
    [Trait("Requirement", "COL-EVT-03")]
    [Trait("Requirement", "COL-STA-02")]
    public void EventDetails_cancel_prevents_closing_while_uncontrolled()
    {
        int calls = 0;
        IRenderedComponent<CollapsibleRoot> cut = Context.Render<CollapsibleRoot>(p => p
            .Add(x => x.DefaultOpen, true)
            .Add(x => x.OnOpenChange, (ValueChangeEventArgs<bool> args) =>
            {
                calls++;
                args.Details.Cancel();
            })
            .AddChildContent<CollapsibleTrigger>(trigger => trigger.AddChildContent("Toggle"))
            .AddChildContent<CollapsiblePanel>(panel => panel.AddChildContent(PanelContent)));

        cut.Find("button").Click();

        Assert.Equal(1, calls);
        Assert.Equal("true", cut.Find("button").GetAttribute("aria-expanded"));
        Assert.Contains(PanelContent, cut.Markup, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "controlled trigger presses request open and close state changes")]
    [Trait("Requirement", "COL-STA-03")]
    public void Controlled_trigger_presses_request_open_and_close_state_changes()
    {
        IRenderedComponent<CollapsibleConsumers> cut = Context.Render<CollapsibleConsumers>(p => p.Add(x => x.Mode, CollapsibleConsumers.ConsumerMode.Applying));

        Assert.Equal("false", cut.Find("button").GetAttribute("aria-expanded"));
        Assert.DoesNotContain(PanelContent, cut.Markup, StringComparison.Ordinal);

        cut.Find("button").Click();
        Assert.Equal("true", cut.Find("button").GetAttribute("aria-expanded"));
        Assert.Contains(PanelContent, cut.Markup, StringComparison.Ordinal);

        cut.Find("button").Click();
        Assert.Equal("false", cut.Find("button").GetAttribute("aria-expanded"));
        host.CompletePanelExit(cut);
        Assert.DoesNotContain(PanelContent, cut.Markup, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "does not change controlled open state without an external update")]
    [Trait("Requirement", "COL-API-01")]
    [Trait("Requirement", "COL-STA-03")]
    [Trait("Requirement", "COL-EVT-01")]
    public void Does_not_change_controlled_open_state_without_an_external_update()
    {
        int calls = 0;
        IRenderedComponent<CollapsibleRoot> cut = Context.Render<CollapsibleRoot>(p => p
            .Add(x => x.Open, false)
            .Add(x => x.OnOpenChange, (ValueChangeEventArgs<bool> _) => calls++)
            .AddChildContent<CollapsibleTrigger>(trigger => trigger.AddChildContent("trigger"))
            .AddChildContent<CollapsiblePanel>(panel => panel.AddChildContent(PanelContent)));

        cut.Find("button").Click();

        Assert.Equal(1, calls);
        Assert.Equal("false", cut.Find("button").GetAttribute("aria-expanded"));
        Assert.DoesNotContain(PanelContent, cut.Markup, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "controlled mode")]
    [Trait("Requirement", "COL-API-03")]
    [Trait("Requirement", "COL-STA-01")]
    [Trait("Requirement", "COL-ARIA-02")]
    [Trait("Requirement", "COL-DOM-04")]
    [Trait("Requirement", "COL-DOM-11")]
    [Trait("Requirement", "COL-EVT-06")]
    public void Controlled_mode()
    {
        IRenderedComponent<CollapsibleConsumers> cut = Context.Render<CollapsibleConsumers>(p => p.Add(x => x.Mode, CollapsibleConsumers.ConsumerMode.External));
        IElement Trigger() => cut.Find("button:not([data-external])");

        Assert.False(Trigger().HasAttribute("aria-controls"));
        Assert.Equal("false", Trigger().GetAttribute("aria-expanded"));
        Assert.DoesNotContain(PanelContent, cut.Markup, StringComparison.Ordinal);

        cut.Find("[data-external]").Click();

        Assert.Equal("true", Trigger().GetAttribute("aria-expanded"));
        Assert.True(Trigger().HasAttribute("aria-controls"));
        Assert.True(cut.Find("[data-panel]").HasAttribute("data-open"));
        Assert.True(Trigger().HasAttribute("data-panel-open"));

        cut.Find("[data-external]").Click();

        Assert.False(Trigger().HasAttribute("aria-controls"));
        Assert.Equal("false", Trigger().GetAttribute("aria-expanded"));
        host.CompletePanelExit(cut);
        Assert.DoesNotContain(PanelContent, cut.Markup, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "uncontrolled mode")]
    [Trait("Requirement", "COL-API-02")]
    [Trait("Requirement", "COL-STA-02")]
    [Trait("Requirement", "COL-DOM-04")]
    [Trait("Requirement", "COL-DOM-16")]
    [Trait("Requirement", "COL-PTR-01")]
    public void Uncontrolled_mode()
    {
        IRenderedComponent<CollapsibleRoot> cut = Context.Render<CollapsibleRoot>(p => p
            .Add(x => x.DefaultOpen, false)
            .AddChildContent<CollapsibleTrigger>()
            .AddChildContent<CollapsiblePanel>(panel => panel.AddChildContent(PanelContent)));

        Assert.False(cut.Find("button").HasAttribute("aria-controls"));
        Assert.Equal("false", cut.Find("button").GetAttribute("aria-expanded"));
        Assert.DoesNotContain(PanelContent, cut.Markup, StringComparison.Ordinal);

        cut.Find("button").Click();

        Assert.Equal("true", cut.Find("button").GetAttribute("aria-expanded"));
        Assert.True(cut.Find("button").HasAttribute("aria-controls"));
        Assert.Contains(PanelContent, cut.Markup, StringComparison.Ordinal);
        Assert.True(cut.Find("div > div").HasAttribute("data-open"));
        Assert.True(cut.Find("button").HasAttribute("data-panel-open"));

        cut.Find("button").Click();

        Assert.Equal("false", cut.Find("button").GetAttribute("aria-expanded"));
        Assert.False(cut.Find("button").HasAttribute("aria-controls"));
        Assert.False(cut.Find("button").HasAttribute("data-panel-open"));
        host.CompletePanelExit(cut);
        Assert.DoesNotContain(PanelContent, cut.Markup, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "passes state to className and style callbacks")]
    [Trait("Requirement", "COL-STA-07")]
    [Trait("Requirement", "COL-EDGE-10")]
    [Trait("Requirement", "COL-DOM-01")]
    public void Passes_state_to_className_and_style_callbacks()
    {
        IRenderedComponent<CollapsibleRoot> cut = Context.Render<CollapsibleRoot>(p => p
            .AddUnmatched("data-testid", "root")
            .Add(x => x.ClassValue, (Func<CollapsibleRootState, string?>)(state => state.Open ? "root-open" : "root-closed"))
            .Add(x => x.StyleValue, (Func<CollapsibleRootState, string?>)(state => state.Open ? "opacity: 1" : "opacity: 0.5"))
            .AddChildContent<CollapsibleTrigger>(trigger => trigger
                .Add(x => x.ClassValue, (Func<CollapsibleTriggerState, string?>)(state => state.Open ? "trigger-open" : "trigger-closed"))
                .Add(x => x.StyleValue, (Func<CollapsibleTriggerState, string?>)(state => state.Open ? "opacity: 1" : "opacity: 0.5"))
                .AddChildContent("Trigger"))
            .AddChildContent<CollapsiblePanel>(panel => panel
                .Add(x => x.KeepMounted, true)
                .AddUnmatched("data-testid", "panel")
                .Add(x => x.ClassValue, (Func<CollapsiblePanelState, string?>)(state => state.Open ? "panel-open" : "panel-closed"))
                .Add(x => x.StyleValue, (Func<CollapsiblePanelState, string?>)(state => state.Open ? "opacity: 1" : "opacity: 0.5"))
                .AddChildContent(PanelContent)));

        Assert.True(cut.Find("[data-testid=root]").ClassList.Contains("root-closed"));
        Assert.Contains("opacity: 0.5", cut.Find("[data-testid=root]").GetAttribute("style"), StringComparison.Ordinal);
        Assert.True(cut.Find("button").ClassList.Contains("trigger-closed"));
        Assert.Contains("opacity: 0.5", cut.Find("button").GetAttribute("style"), StringComparison.Ordinal);
        Assert.True(cut.Find("[data-testid=panel]").ClassList.Contains("panel-closed"));
        Assert.Contains("opacity: 0.5", cut.Find("[data-testid=panel]").GetAttribute("style"), StringComparison.Ordinal);

        cut.Find("button").Click();

        Assert.True(cut.Find("[data-testid=root]").ClassList.Contains("root-open"));
        Assert.Contains("opacity: 1", cut.Find("[data-testid=root]").GetAttribute("style"), StringComparison.Ordinal);
        Assert.True(cut.Find("button").ClassList.Contains("trigger-open"));
        Assert.Contains("opacity: 1", cut.Find("button").GetAttribute("style"), StringComparison.Ordinal);
        Assert.True(cut.Find("[data-testid=panel]").ClassList.Contains("panel-open"));
        // The panel hands its style over at attach (COL-DOM-18, ADR 04 Amendments 2026-10-18): the style function's new
        // result travels to the module with the update, which composes the element's style from it.
        Assert.Contains("\"style\":\"opacity: 1\"", host.UpdateOptions(host.CollapsibleHandle)[^1], StringComparison.Ordinal);
    }

    [Theory(DisplayName = "key: $key should toggle the Collapsible")]
    [InlineData("Enter")]
    [InlineData("Space")]
    [Trait("Requirement", "COL-DOM-09")]
    public void Key_should_toggle_the_Collapsible(string key)
    {
        // At this layer: the enabled trigger is a tab stop (tabindex 0, COL-DOM-09) and the click its key produces toggles;
        // the key itself is pressed at L4.
        _ = key;
        IRenderedComponent<CollapsibleRoot> cut = Context.Render<CollapsibleRoot>(p => p
            .AddChildContent<CollapsibleTrigger>(trigger => trigger.AddChildContent("Trigger"))
            .AddChildContent<CollapsiblePanel>(panel => panel.AddChildContent(PanelContent)));

        Assert.Equal("0", cut.Find("button").GetAttribute("tabindex"));
        cut.Find("button").Click(new MouseEventArgs { Type = "click", Detail = 0 });
        Assert.Equal("true", cut.Find("button").GetAttribute("aria-expanded"));
        Assert.Contains(PanelContent, cut.Markup, StringComparison.Ordinal);
    }
}
