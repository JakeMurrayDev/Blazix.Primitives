using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// Trigger registration, data forwarding and the active-trigger reconciliation at L2 (utils/popups/popupStoreUtils.test.tsx,
/// titles verbatim; PSH-STA-15..29, PSH-ALG-03..05, PSH-DIS-05), over the test-only hosts. Where Base UI writes the store
/// from the test, these tests write it on the renderer's context. Registration happens at the trigger's parameter set
/// (settled by the orchestrator 2026-10-27 under ADR 01 Amendments 2026-10-22) and the reconciliation at the render-batch
/// boundary (ADR 01 decision 3's amendment); each reconciliation title runs under both wirings of design point 8 (pending
/// the user): the root renders on the reconciliation's inputs, or the first popup part to render runs it. Each tree holds a
/// test popup, as every family's markup holds a part that renders on open and close.
/// </summary>
public sealed class PopupStoreUtilsTests : IDisposable
{
    private readonly PopupTestContext test = new();
    private TestPopupRoot<string>? root;

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    private TestPopupStore<string> Store => root!.Store;

    [Fact(DisplayName = "registers and unregisters closed triggers through the context map without notifying the store")]
    [Trait("Requirement", "PSH-STA-17")]
    [Trait("Requirement", "PSH-STA-27")]
    public async Task Registers_and_unregisters_closed_triggers_through_the_context_map_without_notifying_the_store()
    {
        IRenderedComponent<PopupTestHost> cut = RenderTree(triggers: ["trigger"]);
        int notifications = 0;
        Store.Subscribe(_ => notifications++);
        PopupTriggerElement? registered = Store.Triggers.GetById("trigger");

        // Base UI registers the same element three times; here the trigger re-registers on each of three renders.
        for (int i = 0; i < 3; i++)
        {
            await cut.Instance.SetAsync("tick", i);
        }

        Assert.NotNull(registered);
        Assert.Equal(registered, Store.Triggers.GetById("trigger"));
        Assert.True(Store.Triggers.HasElement(registered.Value));
        Assert.Equal(0, Store.Popup.TriggerCount);

        await cut.Instance.SetAsync("triggers", Array.Empty<string>());

        Assert.Null(Store.Triggers.GetById("trigger"));
        Assert.False(Store.Triggers.HasElement(registered.Value));
        Assert.Equal(0, Store.Popup.TriggerCount);
        Assert.Equal(0, notifications);
    }

    [Fact(DisplayName = "re-registers closed triggers when the trigger id changes without notifying the store")]
    [Trait("Requirement", "PSH-STA-26")]
    public async Task Re_registers_closed_triggers_when_the_trigger_id_changes_without_notifying_the_store()
    {
        IRenderedComponent<PopupTestHost> cut = RenderTree(triggers: ["first"]);
        int notifications = 0;
        Store.Subscribe(_ => notifications++);
        TestPopupTrigger<string> trigger = cut.FindComponent<TestPopupTrigger<string>>().Instance;

        Assert.NotNull(Store.Triggers.GetById("first"));

        await cut.Instance.SetAsync("ids", new Dictionary<string, string> { ["first"] = "second" });

        Assert.Same(trigger, cut.FindComponent<TestPopupTrigger<string>>().Instance);
        Assert.Null(Store.Triggers.GetById("first"));
        Assert.NotNull(Store.Triggers.GetById("second"));
        Assert.Equal(0, Store.Popup.TriggerCount);

        await cut.Instance.SetAsync("triggers", Array.Empty<string>());

        Assert.Null(Store.Triggers.GetById("second"));
        Assert.Equal(0, Store.Triggers.Count);
        Assert.Equal(0, notifications);
    }

    [Fact(DisplayName = "follows an id change")]
    [Trait("Requirement", "PSH-STA-26")]
    public async Task Follows_an_id_change()
    {
        IRenderedComponent<PopupTestHost> cut = RenderTree(triggers: ["first"]);

        Assert.NotNull(Store.Triggers.GetById("first"));

        await cut.Instance.SetAsync("ids", new Dictionary<string, string> { ["first"] = "second" });

        Assert.Null(Store.Triggers.GetById("first"));
        Assert.NotNull(Store.Triggers.GetById("second"));
        Assert.Equal(1, Store.Triggers.Count);
        Assert.Equal("second", cut.Find("button").Id);

        await cut.Instance.SetAsync("triggers", Array.Empty<string>());
        Assert.Equal(0, Store.Triggers.Count);
    }

    // Base UI's "registers once the id resolves after the first commit" covers React 17's useId, which is undefined until an
    // effect commits; the port's trigger draws its id before its first parameter set completes (RND-DOM-27), so it registers
    // at once, under the id it renders, before its first render's element exists, and re-registers with that element.
    [Fact(DisplayName = "port: registers under the id it renders at its first parameter set, and again with its element after its first render")]
    [Trait("Requirement", "PSH-STA-26")]
    [Trait("Requirement", "PSH-API-08")]
    public void Registers_under_the_id_it_renders_at_its_first_parameter_set_and_again_with_its_element_after_its_first_render()
    {
        IRenderedComponent<PopupTestHost> cut = RenderTree(triggers: [null]);
        TestPopupTrigger<string> trigger = cut.FindComponent<TestPopupTrigger<string>>().Instance;

        string id = Assert.IsType<string>(trigger.TriggerId);
        PopupTriggerElement registered = Assert.IsType<PopupTriggerElement>(Store.Triggers.GetById(id));
        Assert.StartsWith("base-ui-", id, StringComparison.Ordinal);
        Assert.Same(trigger, registered.Owner);
        Assert.Equal(trigger.Element?.Id, registered.Element?.Id);
        Assert.Equal(1, Store.Triggers.Count);
    }

    [Fact(DisplayName = "keeps triggerCount reactive while the popup is open")]
    [Trait("Requirement", "PSH-STA-17")]
    [Trait("Requirement", "PSH-ALG-04")]
    public async Task Keeps_triggerCount_reactive_while_the_popup_is_open()
    {
        IRenderedComponent<PopupTestHost> cut = RenderTree(triggers: [], defaultOpen: true);

        await cut.Instance.SetAsync("triggers", new[] { "trigger" });

        Assert.NotNull(Store.Triggers.GetById("trigger"));
        Assert.Equal(1, Store.Popup.TriggerCount);

        await cut.Instance.SetAsync("triggers", Array.Empty<string>());

        Assert.Null(Store.Triggers.GetById("trigger"));
        Assert.Equal(0, Store.Popup.TriggerCount);
    }

    [Theory(DisplayName = "claims the only registered trigger when a closed popup opens")]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Requirement", "PSH-STA-18")]
    [Trait("Requirement", "PSH-ALG-03")]
    public async Task Claims_the_only_registered_trigger_when_a_closed_popup_opens(bool rootRendersOnInputs)
    {
        IRenderedComponent<PopupTestHost> cut = RenderTree(triggers: ["trigger"], rootRendersOnInputs: rootRendersOnInputs);
        PopupTriggerElement? element = Store.Triggers.GetById("trigger");

        Assert.Equal(0, Store.Popup.TriggerCount);
        Assert.Null(Store.Popup.ActiveTriggerId);

        // Base UI's store.set('open', true): an open that bypasses the open-change sequence, as a controlled prop does.
        await cut.InvokeAsync(() => Store.WritePopup(Store.Popup with { Open = true }));

        Assert.Equal(1, Store.Popup.TriggerCount);
        Assert.Equal("trigger", Store.Popup.ActiveTriggerId);
        Assert.Equal(element, Store.Popup.ActiveTriggerElement);
    }

    [Theory(DisplayName = "does not claim the only registered trigger when the popup opened without a trigger")]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Requirement", "PSH-STA-19")]
    public async Task Does_not_claim_the_only_registered_trigger_when_the_popup_opened_without_a_trigger(bool rootRendersOnInputs)
    {
        IRenderedComponent<PopupTestHost> cut = RenderTree(triggers: ["trigger"], rootRendersOnInputs: rootRendersOnInputs);

        await cut.InvokeAsync(() => Store.WritePopup(TestPopupStore<string>.CreateOpenState(Store.Popup, open: true, trigger: null, triggerId: null, preventUnmountOnClose: false)));

        Assert.Equal(1, Store.Popup.TriggerCount);
        Assert.True(Store.Popup.OpenedWithoutTrigger);
        Assert.Null(Store.Popup.ActiveTriggerId);
        Assert.Null(Store.Popup.ActiveTriggerElement);
    }

    [Theory(DisplayName = "claims the only registered trigger again after a trigger-less open is closed by a controlled prop")]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Requirement", "PSH-STA-16")]
    public async Task Claims_the_only_registered_trigger_again_after_a_trigger_less_open_is_closed_by_a_controlled_prop(bool rootRendersOnInputs)
    {
        IRenderedComponent<PopupTestHost> cut = RenderTree(triggers: ["trigger"], rootRendersOnInputs: rootRendersOnInputs);
        PopupTriggerElement? element = Store.Triggers.GetById("trigger");

        await cut.InvokeAsync(() => Store.WritePopup(TestPopupStore<string>.CreateOpenState(Store.Popup, open: true, trigger: null, triggerId: null, preventUnmountOnClose: false)));
        Assert.Null(Store.Popup.ActiveTriggerId);

        // A controlled close bypasses CreateOpenState, so the root clears the flag itself.
        await cut.InvokeAsync(() => Store.WritePopup(Store.Popup with { Open = false }));
        Assert.False(Store.Popup.OpenedWithoutTrigger);

        await cut.InvokeAsync(() => Store.WritePopup(Store.Popup with { Open = true }));
        Assert.Equal("trigger", Store.Popup.ActiveTriggerId);
        Assert.Equal(element, Store.Popup.ActiveTriggerElement);
    }

    [Fact(DisplayName = "does not let a trigger registering into an open trigger-less popup claim it")]
    [Trait("Requirement", "PSH-STA-19")]
    [Trait("Requirement", "PSH-ALG-05")]
    public async Task Does_not_let_a_trigger_registering_into_an_open_trigger_less_popup_claim_it()
    {
        IRenderedComponent<PopupTestHost> cut = RenderTree(triggers: []);
        await cut.InvokeAsync(() =>
        {
            Store.SetPayload("programmatic");
            Store.WritePopup(TestPopupStore<string>.CreateOpenState(Store.Popup, open: true, trigger: null, triggerId: null, preventUnmountOnClose: false));
        });

        await cut.Instance.SetAsync("payloads", new Dictionary<string, string> { ["trigger"] = "from trigger" });
        await cut.Instance.SetAsync("triggers", new[] { "trigger" });

        Assert.NotNull(Store.Triggers.GetById("trigger"));
        Assert.Null(Store.Popup.ActiveTriggerId);
        Assert.Null(Store.Popup.ActiveTriggerElement);
        Assert.Equal("programmatic", Store.Payload);
    }

    [Fact(DisplayName = "lets a trigger registering into a popup opened by a controlled prop claim it")]
    [Trait("Requirement", "PSH-STA-15")]
    [Trait("Requirement", "PSH-STA-29")]
    [Trait("Requirement", "PSH-ALG-05")]
    public async Task Lets_a_trigger_registering_into_a_popup_opened_by_a_controlled_prop_claim_it()
    {
        IRenderedComponent<PopupTestHost> cut = RenderTree(triggers: []);
        await cut.InvokeAsync(() => Store.WritePopup(Store.Popup with { Open = true }));

        await cut.Instance.SetAsync("payloads", new Dictionary<string, string> { ["trigger"] = "from trigger" });
        await cut.Instance.SetAsync("triggers", new[] { "trigger" });

        Assert.Equal("trigger", Store.Popup.ActiveTriggerId);
        Assert.Equal(Store.Triggers.GetById("trigger"), Store.Popup.ActiveTriggerElement);
        Assert.Equal("from trigger", Store.Payload);
    }

    [Theory(DisplayName = "closes when the active trigger unregisters while open")]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Requirement", "PSH-STA-25")]
    [Trait("Requirement", "PSH-DIS-05")]
    public async Task Closes_when_the_active_trigger_unregisters_while_open(bool rootRendersOnInputs)
    {
        List<(bool Open, EventReason Reason)> requests = [];
        IRenderedComponent<PopupTestHost> cut = RenderTree(triggers: ["first", "second"], closeOnActiveTriggerUnmount: true, rootRendersOnInputs: rootRendersOnInputs, onOpenChange: args => requests.Add((args.Value, args.Details.Reason)));
        await cut.InvokeAsync(() => Store.WritePopup(Store.Popup with { Open = true, ActiveTriggerId = "first", ActiveTriggerElement = Store.Triggers.GetById("first") }));

        Assert.Equal(2, Store.Popup.TriggerCount);
        Assert.Equal("first", Store.Popup.ActiveTriggerId);

        await cut.Instance.SetAsync("triggers", new[] { "second" });

        (bool open, EventReason reason) = Assert.Single(requests);
        Assert.False(open);
        Assert.Equal(EventReason.None, reason);
        Assert.Equal(0, Store.Popup.TriggerCount);
        Assert.Null(Store.Popup.ActiveTriggerId);
        Assert.Null(Store.Popup.ActiveTriggerElement);
        Assert.False(Store.Popup.Open);
    }

    [Theory(DisplayName = "keeps the popup open when ownership returns to a pending trigger after %s")]
    [InlineData("a different unresolved id", false)]
    [InlineData("a different unresolved id", true)]
    [InlineData("no active trigger", false)]
    [InlineData("no active trigger", true)]
    [Trait("Requirement", "PSH-STA-24")]
    public async Task Keeps_the_popup_open_when_ownership_returns_to_a_pending_trigger_after(string description, bool rootRendersOnInputs)
    {
        string? pendingTriggerId = description == "a different unresolved id" ? "second" : null;
        List<bool> requests = [];
        IRenderedComponent<PopupTestHost> cut = RenderTree(triggers: ["first"], closeOnActiveTriggerUnmount: true, rootRendersOnInputs: rootRendersOnInputs, onOpenChange: args => requests.Add(args.Value));
        await cut.InvokeAsync(() => Store.WritePopup(Store.Popup with { Open = true, ActiveTriggerId = "first", ActiveTriggerElement = Store.Triggers.GetById("first") }));

        await cut.InvokeAsync(() =>
        {
            Store.Triggers.Delete("first");
            Store.WritePopup(Store.Popup with { ActiveTriggerId = pendingTriggerId, ActiveTriggerElement = null, TriggerCount = 0 });
        });
        await cut.Instance.SetAsync("triggers", Array.Empty<string>());
        await cut.InvokeAsync(() => Store.WritePopup(Store.Popup with { ActiveTriggerId = "first", ActiveTriggerElement = null }));
        await cut.InvokeAsync(() => Store.AtBatchBoundaryAsync());

        Assert.True(Store.Popup.Open);
        Assert.Empty(requests);

        await cut.Instance.SetAsync("triggers", new[] { "first" });

        Assert.NotNull(Store.Triggers.GetById("first"));
        Assert.True(Store.Popup.Open);
        Assert.Empty(requests);
    }

    [Theory(DisplayName = "keeps the popup open when the active trigger is replaced with the same id")]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Requirement", "PSH-STA-21")]
    public async Task Keeps_the_popup_open_when_the_active_trigger_is_replaced_with_the_same_id(bool rootRendersOnInputs)
    {
        List<bool> requests = [];
        IRenderedComponent<PopupTestHost> cut = RenderTree(triggers: ["first", "second"], closeOnActiveTriggerUnmount: true, rootRendersOnInputs: rootRendersOnInputs, onOpenChange: args => requests.Add(args.Value));
        PopupTriggerElement? first = Store.Triggers.GetById("first");
        await cut.InvokeAsync(() => Store.WritePopup(Store.Popup with { Open = true, ActiveTriggerId = "first", ActiveTriggerElement = first }));

        Assert.Equal(2, Store.Popup.TriggerCount);

        // A new trigger instance under the same id replaces the first in one batch (Base UI's key change).
        await cut.Instance.SetAsync("keys", new Dictionary<string, string> { ["first"] = "replacement" });

        PopupTriggerElement? replacement = Store.Triggers.GetById("first");
        Assert.NotEqual(first, replacement);
        Assert.NotNull(Store.Triggers.GetById("second"));
        Assert.Equal(2, Store.Popup.TriggerCount);
        Assert.Equal("first", Store.Popup.ActiveTriggerId);
        Assert.Equal(replacement, Store.Popup.ActiveTriggerElement);
        Assert.True(Store.Popup.Open);
        Assert.Empty(requests);
    }

    [Theory(DisplayName = "keeps the popup open when the active trigger element is registered with another id")]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Requirement", "PSH-STA-20")]
    public async Task Keeps_the_popup_open_when_the_active_trigger_element_is_registered_with_another_id(bool rootRendersOnInputs)
    {
        List<bool> requests = [];
        IRenderedComponent<PopupTestHost> cut = RenderTree(triggers: ["registered-id"], closeOnActiveTriggerUnmount: true, rootRendersOnInputs: rootRendersOnInputs, onOpenChange: args => requests.Add(args.Value));
        PopupTriggerElement? element = Store.Triggers.GetById("registered-id");

        await cut.InvokeAsync(() => Store.WritePopup(Store.Popup with { Open = true, ActiveTriggerId = "dom-id", ActiveTriggerElement = element }));
        await cut.InvokeAsync(() => Store.AtBatchBoundaryAsync());

        Assert.Equal(1, Store.Popup.TriggerCount);
        Assert.Equal("registered-id", Store.Popup.ActiveTriggerId);
        Assert.Equal(element, Store.Popup.ActiveTriggerElement);
        Assert.True(Store.Popup.Open);
        Assert.Empty(requests);
    }

    [Theory(DisplayName = "reassociates the active trigger when ownership moves to another rendered-id trigger while open")]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Requirement", "PSH-STA-20")]
    public async Task Reassociates_the_active_trigger_when_ownership_moves_to_another_rendered_id_trigger_while_open(bool rootRendersOnInputs)
    {
        List<bool> requests = [];
        IRenderedComponent<PopupTestHost> cut = RenderTree(triggers: ["registered-1", "registered-2"], closeOnActiveTriggerUnmount: true, rootRendersOnInputs: rootRendersOnInputs, onOpenChange: args => requests.Add(args.Value));
        await cut.InvokeAsync(() => Store.WritePopup(Store.Popup with { Open = true, ActiveTriggerId = "registered-1", ActiveTriggerElement = Store.Triggers.GetById("registered-1") }));
        await cut.InvokeAsync(() => Store.AtBatchBoundaryAsync());

        Assert.Equal("registered-1", Store.Popup.ActiveTriggerId);

        // A handoff names the second trigger by an id it is not registered under, with its element.
        await cut.InvokeAsync(() => Store.WritePopup(Store.Popup with { ActiveTriggerId = "dom-id-2", ActiveTriggerElement = Store.Triggers.GetById("registered-2") }));
        await cut.InvokeAsync(() => Store.AtBatchBoundaryAsync());

        Assert.Equal("registered-2", Store.Popup.ActiveTriggerId);
        Assert.Equal(Store.Triggers.GetById("registered-2"), Store.Popup.ActiveTriggerElement);
        Assert.True(Store.Popup.Open);
        Assert.Empty(requests);
    }

    [Theory(DisplayName = "preserves active trigger ownership without closing by default")]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Requirement", "PSH-STA-22")]
    [Trait("Requirement", "PSH-ALG-03")]
    public async Task Preserves_active_trigger_ownership_without_closing_by_default(bool rootRendersOnInputs)
    {
        List<bool> requests = [];
        IRenderedComponent<PopupTestHost> cut = RenderTree(triggers: ["first", "second"], rootRendersOnInputs: rootRendersOnInputs, onOpenChange: args => requests.Add(args.Value));
        PopupTriggerElement? first = Store.Triggers.GetById("first");
        await cut.InvokeAsync(() => Store.WritePopup(Store.Popup with { Open = true, ActiveTriggerId = "first", ActiveTriggerElement = first }));

        Assert.Equal(2, Store.Popup.TriggerCount);

        await cut.Instance.SetAsync("triggers", new[] { "second" });

        Assert.Empty(requests);
        Assert.True(Store.Popup.Open);
        Assert.Null(Store.Triggers.GetById("first"));
        Assert.NotNull(Store.Triggers.GetById("second"));
        Assert.Equal(1, Store.Popup.TriggerCount);
        Assert.Equal("first", Store.Popup.ActiveTriggerId);
        Assert.Equal(first, Store.Popup.ActiveTriggerElement);
    }

    [Theory(DisplayName = "resets triggerCount when the popup closes")]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Requirement", "PSH-STA-16")]
    [Trait("Requirement", "PSH-ALG-03")]
    public async Task Resets_triggerCount_when_the_popup_closes(bool rootRendersOnInputs)
    {
        IRenderedComponent<PopupTestHost> cut = RenderTree(triggers: ["trigger"], defaultOpen: true, rootRendersOnInputs: rootRendersOnInputs);

        Assert.Equal(1, Store.Popup.TriggerCount);

        await cut.InvokeAsync(() => Store.WritePopup(Store.Popup with { Open = false }));

        Assert.Equal(0, Store.Popup.TriggerCount);
    }

    // Renders a root of string payloads around the triggers the host's "triggers" value names (null for a trigger without an
    // id); "ids" renames a trigger's id, "keys" gives one a new instance under the same id, "payloads" gives one a payload.
    private IRenderedComponent<PopupTestHost> RenderTree(
        string?[] triggers,
        bool defaultOpen = false,
        bool closeOnActiveTriggerUnmount = false,
        bool rootRendersOnInputs = false,
        Action<ValueChangeEventArgs<bool>>? onOpenChange = null)
    {
        IRenderedComponent<PopupTestHost> cut = test.Render((builder, host) =>
        {
            builder.OpenComponent<TestPopupRoot<string>>(0);
            builder.AddComponentParameter(1, nameof(TestPopupRoot<string>.DefaultOpen), defaultOpen);
            builder.AddComponentParameter(2, nameof(TestPopupRoot<string>.CloseOnActiveTriggerUnmount), closeOnActiveTriggerUnmount);
            builder.AddComponentParameter(3, nameof(TestPopupRoot<string>.RendersOnReconciliationInputs), rootRendersOnInputs);
            if (onOpenChange is not null)
            {
                builder.AddComponentParameter(4, nameof(TestPopupRoot<string>.OnOpenChange), EventCallback.Factory.Create(this, onOpenChange));
            }

            builder.AddComponentParameter(5, nameof(TestPopupRoot<string>.ChildContent), (RenderFragment)(content => RenderTriggers(content, host)));
            builder.AddComponentReferenceCapture(6, reference => root = (TestPopupRoot<string>)reference);
            builder.CloseComponent();
        }, new() { ["triggers"] = triggers });
        return cut;
    }

    private static void RenderTriggers(RenderTreeBuilder builder, PopupTestHost host)
    {
        string?[] triggers = host.Get<string?[]>("triggers") ?? [];
        Dictionary<string, string> ids = host.Get<Dictionary<string, string>>("ids") ?? [];
        Dictionary<string, string> keys = host.Get<Dictionary<string, string>>("keys") ?? [];
        Dictionary<string, string> payloads = host.Get<Dictionary<string, string>>("payloads") ?? [];
        // Every family renders a popup part that selects the open and mounted state (its Portal and Popup), which is what
        // brings a render-batch boundary after an open that no trigger's state shows (design point 8, option b).
        builder.OpenComponent<TestPopupPopup<string>>(4);
        builder.CloseComponent();
        foreach (string? name in triggers)
        {
            builder.OpenComponent<TestPopupTrigger<string>>(0);
            builder.SetKey(name is not null && keys.TryGetValue(name, out string? key) ? key : name);
            if (name is not null)
            {
                builder.AddComponentParameter(1, "id", ids.TryGetValue(name, out string? renamed) ? renamed : name);
                if (payloads.TryGetValue(name, out string? payload))
                {
                    builder.AddComponentParameter(2, nameof(TestPopupTrigger<string>.Payload), payload);
                }
            }

            builder.AddComponentParameter(3, nameof(TestPopupTrigger<string>.ChildContent), (RenderFragment)(text => text.AddContent(0, name)));
            builder.CloseComponent();
        }
    }
}
