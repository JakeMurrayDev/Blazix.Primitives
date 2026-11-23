using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The preview card root at L2 (PreviewCardRoot.test.tsx, titles verbatim where Base UI has one): its open state controlled
/// and uncontrolled, the imperative close and unmount, the session's dismissal and nesting, the instant type on the popup
/// parts, and the requests the <c>interactions</c> module reports, driven through
/// <see cref="PreviewCardTestContext.RequestAsync"/> (ADR 06 §1). The delays and the hover leave are the module's (L3, L4).
/// </summary>
public sealed class PreviewCardRootTests : IDisposable
{
    private readonly PreviewCardTestContext host = new();

    /// <inheritdoc />
    public void Dispose() => host.Dispose();

    [Fact(DisplayName = "should open when the trigger is hovered")]
    [Trait("Requirement", "PRV-PTR-01")]
    [Trait("Requirement", "PRV-ANI-01")]
    [Trait("Requirement", "PRV-STR-02")]
    public async Task Should_open_when_the_trigger_is_hovered()
    {
        IRenderedComponent<PreviewCardBasicHost> cut = host.Context.Render<PreviewCardBasicHost>();
        Assert.Empty(cut.FindAll("[data-testid=\"popup\"]"));

        await host.RequestAsync(host.TriggerTwin("trigger"), open: true, "trigger-hover", "trigger");

        cut.WaitForAssertion(() => Assert.Contains("Content", cut.Find("[data-testid=\"popup\"]").TextContent, StringComparison.Ordinal));
        Assert.NotNull(cut.Find("[data-testid=\"before-trigger\"]"));
        Assert.NotNull(cut.Find("[data-testid=\"between-trigger-and-portal\"]"));
        Assert.NotNull(cut.Find("[data-testid=\"after-portal\"]"));
        Assert.True(cut.Find("#trigger").HasAttribute("data-popup-open"));
    }

    [Fact(DisplayName = "should close when the trigger is unhovered")]
    [Trait("Requirement", "PRV-STR-03")]
    [Trait("Requirement", "PRV-PTR-04")]
    [Trait("Requirement", "PRV-ANI-01")]
    public async Task Should_close_when_the_trigger_is_unhovered()
    {
        IRenderedComponent<PreviewCardBasicHost> cut = host.Context.Render<PreviewCardBasicHost>();
        await host.RequestAsync(host.TriggerTwin("trigger"), open: true, "trigger-hover", "trigger");
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=\"popup\"]")));

        await host.RequestAsync(host.TriggerTwin("trigger"), open: false, "trigger-hover");

        // The exit is the popup's transitions handle's (bUnit has none to end it): the store has closed and the parts render
        // the closed state.
        Assert.False(cut.Instance.Root!.Store.IsOpen);
        Assert.True(cut.Find("[data-testid=\"popup\"]").HasAttribute("data-closed"));
        Assert.False(cut.Find("#trigger").HasAttribute("data-popup-open"));
    }

    [Fact(DisplayName = "should open when the trigger is focused")]
    [Trait("Requirement", "PRV-KEY-01")]
    [Trait("Requirement", "PRV-A11Y-01")]
    public async Task Should_open_when_the_trigger_is_focused()
    {
        IRenderedComponent<PreviewCardBasicHost> cut = host.Context.Render<PreviewCardBasicHost>();

        await host.RequestAsync(host.TriggerTwin("trigger"), open: true, "trigger-focus", "trigger", eventJson: PreviewCardTestContext.FocusJson);

        cut.WaitForAssertion(() => Assert.Contains("Content", cut.Find("[data-testid=\"popup\"]").TextContent, StringComparison.Ordinal));
        Assert.Equal(PreviewCardInstantType.Focus, cut.Instance.Root!.Store.State.InstantType);
    }

    [Fact(DisplayName = "should close when the trigger is blurred")]
    [Trait("Requirement", "PRV-KEY-03")]
    [Trait("Requirement", "PRV-DIS-06")]
    public async Task Should_close_when_the_trigger_is_blurred()
    {
        IRenderedComponent<PreviewCardBasicHost> cut = host.Context.Render<PreviewCardBasicHost>();
        await host.RequestAsync(host.TriggerTwin("trigger"), open: true, "trigger-focus", "trigger", eventJson: PreviewCardTestContext.FocusJson);
        cut.WaitForAssertion(() => Assert.True(cut.Instance.Root!.Store.IsOpen));

        await host.RequestAsync(host.TriggerTwin("trigger"), open: false, "trigger-focus", eventJson: PreviewCardTestContext.FocusJson);

        Assert.False(cut.Instance.Root!.Store.IsOpen);
        Assert.True(cut.Find("[data-testid=\"popup\"]").HasAttribute("data-closed"));
    }

    [Fact(DisplayName = "should call onOpenChange when the open state changes")]
    [Trait("Requirement", "PRV-API-02")]
    [Trait("Requirement", "PRV-EVT-01")]
    [Trait("Requirement", "PRV-EVT-08")]
    public async Task Should_call_onOpenChange_when_the_open_state_changes()
    {
        IRenderedComponent<PreviewCardControlledHost> cut = host.Context.Render<PreviewCardControlledHost>();
        Assert.Empty(cut.FindAll("[data-testid=\"popup\"]"));

        await host.RequestAsync(host.TriggerTwin("trigger"), open: true, "trigger-hover", "trigger");
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=\"popup\"]")));
        await host.RequestAsync(host.TriggerTwin("trigger"), open: false, "trigger-hover");

        // The handler observes the state before the change (EVD-STA-08): false, then true.
        Assert.Equal([(true, EventReason.TriggerHover, false), (false, EventReason.TriggerHover, true)], cut.Instance.Calls);
        Assert.False(cut.Instance.Root!.Store.IsOpen);
    }

    [Fact(DisplayName = "is ignored when `open={true}` is passed, so Escape cannot close")]
    [Trait("Requirement", "PRV-API-01")]
    [Trait("Requirement", "PRV-DIS-01")]
    [Trait("Requirement", "PRV-DIS-10")]
    public async Task Is_ignored_when_open_true_is_passed_so_Escape_cannot_close()
    {
        IRenderedComponent<PreviewCardControlledHost> cut = host.Context.Render<PreviewCardControlledHost>(p => p.Add(x => x.Open, true).Add(x => x.Accept, false));

        await host.RequestAsync(host.SessionTwin(), open: false, "escape-key", eventJson: null);

        Assert.Equal([(false, EventReason.EscapeKey, true)], cut.Instance.Calls);
        Assert.True(cut.Instance.Root!.Store.IsOpen);
        Assert.NotNull(cut.Find("[data-testid=\"popup\"]"));
    }

    [Fact(DisplayName = "keeps the popup closed when the open change is canceled")]
    [Trait("Requirement", "PRV-DOM-02")]
    [Trait("Requirement", "PRV-EVT-06")]
    [Trait("Requirement", "PRV-EDGE-10")]
    public async Task Keeps_the_popup_closed_when_the_open_change_is_canceled()
    {
        List<EventReason> reasons = [];
        IRenderedComponent<PreviewCardBasicHost> cut = host.Context.Render<PreviewCardBasicHost>(p => p.Add(
            x => x.OnOpenChange,
            EventCallback.Factory.Create<ValueChangeEventArgs<bool>>(this, args =>
            {
                reasons.Add(args.Details.Reason);
                args.Details.Cancel();
            })));

        await host.RequestAsync(host.TriggerTwin("trigger"), open: true, "trigger-hover", "trigger");

        Assert.Equal([EventReason.TriggerHover], reasons);
        Assert.False(cut.Instance.Root!.Store.IsOpen);
        Assert.Null(cut.Instance.Root.Store.State.InstantType);
        Assert.Empty(cut.FindAll("[data-testid=\"popup\"]"));
        Assert.False(cut.Find("#trigger").HasAttribute("data-popup-open"));
    }

    [Fact(DisplayName = "closes when the trigger is unhovered after opening by default")]
    [Trait("Requirement", "PRV-PTR-04")]
    [Trait("Requirement", "PSH-STA-18")]
    public async Task Closes_when_the_trigger_is_unhovered_after_opening_by_default()
    {
        IRenderedComponent<PreviewCardBasicHost> cut = host.Context.Render<PreviewCardBasicHost>(p => p.Add(x => x.DefaultOpen, true));
        cut.WaitForAssertion(() => Assert.True(cut.Find("#trigger").HasAttribute("data-popup-open")));

        await host.RequestAsync(host.TriggerTwin("trigger"), open: false, "trigger-hover");

        cut.WaitForAssertion(() => Assert.False(cut.Instance.Root!.Store.IsOpen));
        Assert.False(cut.Find("#trigger").HasAttribute("data-popup-open"));
    }

    [Fact(DisplayName = "reopens on hover after Escape closes it")]
    [Trait("Requirement", "PRV-PTR-09")]
    [Trait("Requirement", "PRV-DIS-01")]
    public async Task Reopens_on_hover_after_Escape_closes_it()
    {
        IRenderedComponent<PreviewCardBasicHost> cut = host.Context.Render<PreviewCardBasicHost>(p => p.Add(x => x.Delay, 100));
        await host.RequestAsync(host.TriggerTwin("trigger"), open: true, "trigger-hover", "trigger");
        cut.WaitForAssertion(() => Assert.True(cut.Instance.Root!.Store.IsOpen));

        await host.RequestAsync(host.SessionTwin(), open: false, "escape-key", eventJson: null);
        Assert.False(cut.Instance.Root!.Store.IsOpen);

        await host.RequestAsync(host.TriggerTwin("trigger"), open: true, "trigger-hover", "trigger");

        cut.WaitForAssertion(() => Assert.True(cut.Find("[data-testid=\"popup\"]").HasAttribute("data-open")));
    }

    [Fact(DisplayName = "unmounts the preview card when the `unmount` method is called")]
    [Trait("Requirement", "PRV-API-05")]
    [Trait("Requirement", "PRV-EVT-07")]
    public async Task Unmounts_the_preview_card_when_the_unmount_method_is_called()
    {
        IRenderedComponent<PreviewCardBasicHost> cut = host.Context.Render<PreviewCardBasicHost>(p => p
            .Add(x => x.Delay, 0)
            .Add(x => x.CloseDelay, 0)
            .Add(x => x.OnOpenChange, EventCallback.Factory.Create<ValueChangeEventArgs<bool>>(this, args => ((OpenChangeEventDetails)args.Details).PreventUnmountOnClose())));
        await host.RequestAsync(host.TriggerTwin("trigger"), open: true, "trigger-hover", "trigger");
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=\"positioner\"]")));

        await host.RequestAsync(host.TriggerTwin("trigger"), open: false, "trigger-hover");
        Assert.NotNull(cut.Find("[data-testid=\"positioner\"]"));

        await cut.InvokeAsync(cut.Instance.Root!.Unmount);

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid=\"positioner\"]")));
    }

    [Fact(DisplayName = "closes the preview card when the `close` method is called")]
    [Trait("Requirement", "PRV-API-03")]
    [Trait("Requirement", "PRV-API-05")]
    [Trait("Requirement", "PRV-DOM-02")]
    [Trait("Requirement", "PRV-DIS-11")]
    [Trait("Requirement", "PRV-EVT-02")]
    public async Task Closes_the_preview_card_when_the_close_method_is_called()
    {
        List<(bool, EventReason)> calls = [];
        IRenderedComponent<PreviewCardBasicHost> cut = host.Context.Render<PreviewCardBasicHost>(p => p.Add(x => x.Delay, 0).Add(
            x => x.OnOpenChange,
            EventCallback.Factory.Create<ValueChangeEventArgs<bool>>(this, args => calls.Add((args.Value, args.Details.Reason)))));
        await host.RequestAsync(host.TriggerTwin("trigger"), open: true, "trigger-hover", "trigger");
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=\"popup\"]")));

        await cut.InvokeAsync(() => cut.Instance.Root!.CloseAsync());

        Assert.False(cut.Find("#trigger").HasAttribute("data-popup-open"));
        Assert.Equal((false, EventReason.ImperativeAction), calls[^1]);
        Assert.True(cut.Find("[data-testid=\"popup\"]").HasAttribute("data-closed"));
    }

    [Fact(DisplayName = "port: the root renders no element, and the popup renders no role, a focusable tabindex, the open state and PreviewCard's default side")]
    [Trait("Requirement", "PRV-STR-01")]
    [Trait("Requirement", "PRV-DOM-12")]
    [Trait("Requirement", "PRV-DOM-13")]
    [Trait("Requirement", "PRV-DOM-15")]
    [Trait("Requirement", "PRV-ARIA-03")]
    public void The_root_renders_no_element_and_the_popup_renders_no_role()
    {
        IRenderedComponent<PreviewCardBasicHost> cut = host.Context.Render<PreviewCardBasicHost>(p => p.Add(x => x.DefaultOpen, true));

        AngleSharp.Dom.IElement popup = cut.Find("[data-testid=\"popup\"]");
        Assert.False(popup.HasAttribute("role"));
        Assert.False(popup.HasAttribute("id"));
        Assert.DoesNotContain(popup.Attributes, attribute => attribute.Name.StartsWith("aria-", StringComparison.Ordinal));
        Assert.Equal("-1", popup.GetAttribute("tabindex"));
        Assert.True(popup.HasAttribute("data-base-ui-focusable"));
        Assert.True(popup.HasAttribute("data-open"));
        Assert.Equal("bottom", popup.GetAttribute("data-side"));
        Assert.Equal("center", popup.GetAttribute("data-align"));
        Assert.False(popup.HasAttribute("data-instant"));
        Assert.Equal("before-trigger", cut.Nodes.OfType<AngleSharp.Dom.IElement>().First().GetAttribute("data-testid"));
    }

    [Fact(DisplayName = "port: a focus open renders data-instant=\"focus\" and an Escape close renders dismiss on the popup and the positioner, but not the arrow")]
    [Trait("Requirement", "PRV-STA-10")]
    [Trait("Requirement", "PRV-DOM-09")]
    [Trait("Requirement", "PRV-DOM-13")]
    [Trait("Requirement", "PRV-DOM-17")]
    public async Task A_focus_open_renders_focus_and_an_Escape_close_renders_dismiss()
    {
        IRenderedComponent<PreviewCardBasicHost> cut = host.Context.Render<PreviewCardBasicHost>();

        await host.RequestAsync(host.TriggerTwin("trigger"), open: true, "trigger-focus", "trigger", eventJson: PreviewCardTestContext.FocusJson);

        Assert.Equal("focus", cut.Find("[data-testid=\"popup\"]").GetAttribute("data-instant"));
        Assert.Equal("focus", cut.Find("[data-testid=\"positioner\"]").GetAttribute("data-instant"));
        Assert.False(cut.Find("[data-testid=\"arrow\"]").HasAttribute("data-instant"));

        await host.RequestAsync(host.SessionTwin(), open: false, "escape-key", eventJson: null);

        Assert.Equal("dismiss", cut.Find("[data-testid=\"popup\"]").GetAttribute("data-instant"));
        Assert.Equal("dismiss", cut.Find("[data-testid=\"positioner\"]").GetAttribute("data-instant"));

        await host.RequestAsync(host.TriggerTwin("trigger"), open: true, "trigger-hover", "trigger");

        Assert.False(cut.Find("[data-testid=\"popup\"]").HasAttribute("data-instant"));
    }

    [Fact(DisplayName = "port: the session holds Escape and the outside press only while the card is open or mounted, or the root has a handle")]
    [Trait("Requirement", "PRV-STA-16")]
    [Trait("Requirement", "PRV-DIS-01")]
    public async Task The_session_holds_dismissal_only_while_open_mounted_or_handled()
    {
        IRenderedComponent<PreviewCardBasicHost> cut = host.Context.Render<PreviewCardBasicHost>();
        Assert.Null(LastDismiss());

        await host.RequestAsync(host.TriggerTwin("trigger"), open: true, "trigger-hover", "trigger");
        cut.WaitForAssertion(() => Assert.Equal("{}", LastDismiss()));

        // A root with a handle holds it while closed too (PSH-STA-43).
        host.Context.Render<PreviewCardDetachedHost>(p => p.Add(x => x.Handle, new PreviewCardHandle<int>()));
        Assert.Equal("{}", LastDismiss());
    }

    [Fact(DisplayName = "port: a nested root's session names the enclosing card's session as its parent")]
    [Trait("Requirement", "PRV-STR-10")]
    [Trait("Requirement", "PRV-STR-01")]
    public void A_nested_roots_session_names_the_enclosing_cards_session()
    {
        host.Context.Render<PreviewCardRoot<NoPayload>>(p => p.Add(x => x.DefaultOpen, true).Add(x => x.ChildContent, (RenderFragment)(outer =>
        {
            outer.OpenComponent<PreviewCardTrigger<NoPayload>>(0);
            outer.AddAttribute(1, "id", "outer-trigger");
            outer.CloseComponent();
            outer.OpenComponent<PreviewCardPortal>(2);
            outer.AddAttribute(3, nameof(PreviewCardPortal.ChildContent), (RenderFragment)(portal =>
            {
                portal.OpenComponent<PreviewCardPositioner>(0);
                portal.AddAttribute(1, nameof(PreviewCardPositioner.ChildContent), (RenderFragment)(positioner =>
                {
                    positioner.OpenComponent<PreviewCardPopup>(0);
                    positioner.AddAttribute(1, nameof(PreviewCardPopup.ChildContent), (RenderFragment)(popup =>
                    {
                        popup.OpenComponent<PreviewCardRoot<NoPayload>>(0);
                        popup.AddAttribute(1, nameof(PreviewCardRoot<NoPayload>.ChildContent), (RenderFragment)(inner =>
                        {
                            inner.OpenComponent<PreviewCardTrigger<NoPayload>>(0);
                            inner.AddAttribute(1, "id", "inner-trigger");
                            inner.CloseComponent();
                        }));
                        popup.CloseComponent();
                    }));
                    positioner.CloseComponent();
                }));
                portal.CloseComponent();
            }));
            outer.CloseComponent();
        })));

        IReadOnlyList<JsonElement> sessions = host.SessionAttaches;
        Assert.Equal(2, sessions.Count);
        string outerToken = sessions[0].GetProperty("sessionToken").GetString()!;
        Assert.False(sessions[0].GetProperty("session").TryGetProperty("parentToken", out _));
        Assert.Equal(outerToken, sessions[1].GetProperty("session").GetProperty("parentToken").GetString());
    }

    [Fact(DisplayName = "port: an open and a close by hover render neither the root nor its content outside the popup")]
    [Trait("Requirement", "PRV-STA-17")]
    [Trait("Requirement", "ADR16-7")]
    public async Task An_open_and_a_close_render_neither_the_root_nor_its_content()
    {
        int contentRenders = 0;
        IRenderedComponent<PreviewCardRoot<NoPayload>> cut = host.Context.Render<PreviewCardRoot<NoPayload>>(p => p.Add(x => x.ChildContent, (RenderFragment)(builder =>
        {
            contentRenders++;
            builder.OpenComponent<PreviewCardTrigger<NoPayload>>(0);
            builder.AddAttribute(1, "id", "trigger");
            builder.CloseComponent();
            builder.OpenComponent<PreviewCardPortal>(2);
            builder.AddAttribute(3, nameof(PreviewCardPortal.ChildContent), (RenderFragment)(portal =>
            {
                portal.OpenComponent<PreviewCardPositioner>(0);
                portal.AddAttribute(1, nameof(PreviewCardPositioner.ChildContent), (RenderFragment)(positioner =>
                {
                    positioner.OpenComponent<PreviewCardPopup>(0);
                    positioner.AddAttribute(1, "data-testid", "popup");
                    positioner.CloseComponent();
                }));
                portal.CloseComponent();
            }));
            builder.CloseComponent();
        })));
        int before = contentRenders;

        await host.RequestAsync(host.TriggerTwin("trigger"), open: true, "trigger-hover", "trigger");
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=\"popup\"]")));
        await host.RequestAsync(host.TriggerTwin("trigger"), open: false, "trigger-hover");
        cut.WaitForAssertion(() => Assert.True(cut.Find("[data-testid=\"popup\"]").HasAttribute("data-closed")));

        // The figure measured on this branch: 0 renders of the root's content for an open and a close.
        Assert.Equal(before, contentRenders);
    }

    private string? LastDismiss()
    {
        string? last = null;
        bool seen = false;
        foreach (string json in host.InteractionsAttaches.Select(attach => (string)attach.Arguments[1]!).Concat(host.InteractionsUpdates))
        {
            JsonElement root = JsonDocument.Parse(json).RootElement;
            if (root.TryGetProperty("session", out JsonElement session))
            {
                seen = true;
                last = session.TryGetProperty("dismiss", out JsonElement dismiss) ? dismiss.GetRawText() : null;
            }
        }

        Assert.True(seen);
        return last;
    }
}
