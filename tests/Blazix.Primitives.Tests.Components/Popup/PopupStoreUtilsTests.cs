using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// Trigger registration, data forwarding and the active-trigger reconciliation at L2 (utils/popups/popupStoreUtils.test.tsx,
/// titles verbatim; PSH-STA-15..29, PSH-ALG-03..05, PSH-DIS-05), over the test-only hosts. Where Base UI writes the store
/// from the test, these tests write it on the renderer's context. Registration happens at the trigger's parameter set
/// (settled by the orchestrator 2026-10-27 under ADR 01 Amendments 2026-10-22) and the reconciliation at the render-batch
/// boundary (ADR 01 decision 3's amendment); each reconciliation title runs under both wirings of design point 8 (ADR 16
/// Amendments 2026-10-27): with a test popup, which selects the open and mounted state as every family's Portal and Popup
/// do and runs the boundary after its renders, and without one, where the store renders the root on the reconciliation's
/// inputs (the shape of Base UI's bare-store tests). The other titles render the family shape, with a test popup.
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

    // Base UI's stable callback is the trigger's registration, which follows the store its trigger reaches: a detached
    // trigger whose handle moves to another root unregisters from the first store and registers into the second, and a new
    // instance under the same id registers only into the current one.
    [Fact(DisplayName = "returns a stable callback that unregisters from the store it registered in")]
    [Trait("Requirement", "PSH-STA-26")]
    [Trait("Requirement", "PSH-ALG-04")]
    public async Task Returns_a_stable_callback_that_unregisters_from_the_store_it_registered_in()
    {
        TestPopupHandle<string> first = new();
        TestPopupHandle<string> second = new();
        TestPopupRoot<string>? firstRoot = null;
        TestPopupRoot<string>? secondRoot = null;
        IRenderedComponent<PopupTestHost> cut = test.Render(
            (builder, host) =>
            {
                builder.OpenComponent<TestPopupRoot<string>>(0);
                builder.AddComponentParameter(1, nameof(TestPopupRoot<string>.Handle), first);
                builder.AddComponentReferenceCapture(2, reference => firstRoot = (TestPopupRoot<string>)reference);
                builder.CloseComponent();
                builder.OpenComponent<TestPopupRoot<string>>(3);
                builder.AddComponentParameter(4, nameof(TestPopupRoot<string>.Handle), second);
                builder.AddComponentReferenceCapture(5, reference => secondRoot = (TestPopupRoot<string>)reference);
                builder.CloseComponent();
                if (host.Get<bool>("mounted"))
                {
                    builder.OpenComponent<TestPopupTrigger<string>>(6);
                    builder.SetKey(host.Get<string>("key"));
                    builder.AddComponentParameter(7, "id", "trigger");
                    builder.AddComponentParameter(8, nameof(TestPopupTrigger<string>.Handle), host.Get<TestPopupHandle<string>>("handle"));
                    builder.CloseComponent();
                }
            },
            new() { ["mounted"] = true, ["key"] = "initial", ["handle"] = first });

        cut.WaitForAssertion(() => Assert.NotNull(firstRoot!.Store.Triggers.GetById("trigger")));
        PopupTriggerElement initial = firstRoot!.Store.Triggers.GetById("trigger")!.Value;

        await cut.Instance.SetAsync("handle", second);

        Assert.Null(firstRoot.Store.Triggers.GetById("trigger"));
        Assert.Equal(initial, secondRoot!.Store.Triggers.GetById("trigger"));

        await cut.Instance.SetAsync("key", "replacement");

        Assert.Null(firstRoot.Store.Triggers.GetById("trigger"));
        PopupTriggerElement? replacement = secondRoot.Store.Triggers.GetById("trigger");
        Assert.NotNull(replacement);
        Assert.NotEqual(initial, replacement);

        await cut.Instance.SetAsync("mounted", false);
        Assert.Null(secondRoot.Store.Triggers.GetById("trigger"));
    }

    [Theory(DisplayName = "claims the only registered trigger when a closed popup opens")]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Requirement", "PSH-STA-18")]
    [Trait("Requirement", "PSH-ALG-03")]
    public async Task Claims_the_only_registered_trigger_when_a_closed_popup_opens(bool withPopupPart)
    {
        IRenderedComponent<PopupTestHost> cut = RenderTree(triggers: ["trigger"], withPopupPart: withPopupPart);
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
    public async Task Does_not_claim_the_only_registered_trigger_when_the_popup_opened_without_a_trigger(bool withPopupPart)
    {
        IRenderedComponent<PopupTestHost> cut = RenderTree(triggers: ["trigger"], withPopupPart: withPopupPart);

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
    public async Task Claims_the_only_registered_trigger_again_after_a_trigger_less_open_is_closed_by_a_controlled_prop(bool withPopupPart)
    {
        IRenderedComponent<PopupTestHost> cut = RenderTree(triggers: ["trigger"], withPopupPart: withPopupPart);
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
    public async Task Closes_when_the_active_trigger_unregisters_while_open(bool withPopupPart)
    {
        List<(bool Open, EventReason Reason)> requests = [];
        IRenderedComponent<PopupTestHost> cut = RenderTree(triggers: ["first", "second"], closeOnActiveTriggerUnmount: true, withPopupPart: withPopupPart, onOpenChange: args => requests.Add((args.Value, args.Details.Reason)));
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

    // Base UI hides the trigger in a layout effect of the claiming commit; here the trigger's first after-render hides it, the
    // batch that follows the boundary that claimed it.
    [Theory(DisplayName = "closes when an implicitly claimed trigger unmounts during the claim commit")]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Requirement", "PSH-STA-18")]
    [Trait("Requirement", "PSH-STA-25")]
    [Trait("Requirement", "PSH-ALG-03")]
    public async Task Closes_when_an_implicitly_claimed_trigger_unmounts_during_the_claim_commit(bool withPopupPart)
    {
        List<(bool Open, EventReason Reason)> requests = [];
        IRenderedComponent<PopupTestHost> cut = RenderTree(
            triggers: ["trigger"],
            defaultOpen: true,
            closeOnActiveTriggerUnmount: true,
            withPopupPart: withPopupPart,
            onOpenChange: args => requests.Add((args.Value, args.Details.Reason)),
            hideTriggersAfterFirstRender: true);

        cut.WaitForAssertion(() => Assert.Single(requests));
        (bool open, EventReason reason) = requests[0];
        Assert.False(open);
        Assert.Equal(EventReason.None, reason);
        Assert.Null(Store.Triggers.GetById("trigger"));
        Assert.Null(Store.Popup.ActiveTriggerId);
        Assert.Null(Store.Popup.ActiveTriggerElement);
        Assert.False(Store.Popup.Open);
    }

    [Theory(DisplayName = "closes when the active trigger unmounts after registering in a count-neutral commit")]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Requirement", "PSH-STA-23")]
    [Trait("Requirement", "PSH-STA-25")]
    [Trait("Requirement", "PSH-ALG-03")]
    public async Task Closes_when_the_active_trigger_unmounts_after_registering_in_a_count_neutral_commit(bool withPopupPart)
    {
        List<(bool Open, EventReason Reason)> requests = [];
        IRenderedComponent<PopupTestHost> cut = RenderTree(triggers: ["first"], closeOnActiveTriggerUnmount: true, withPopupPart: withPopupPart, onOpenChange: args => requests.Add((args.Value, args.Details.Reason)));
        await cut.InvokeAsync(() => Store.WritePopup(Store.Popup with { Open = true, ActiveTriggerId = "first", ActiveTriggerElement = Store.Triggers.GetById("first") }));

        // Ownership moves to a trigger that has not registered yet: the popup stays open (pending).
        await cut.InvokeAsync(() => Store.WritePopup(Store.Popup with { ActiveTriggerId = "second", ActiveTriggerElement = null }));
        Assert.True(Store.Popup.Open);
        Assert.Empty(requests);

        // Swap "first" out and "second" in within one batch, so the trigger count nets out unchanged.
        await cut.Instance.SetAsync("triggers", new[] { "second" });
        Assert.Equal(Store.Triggers.GetById("second"), Store.Popup.ActiveTriggerElement);
        Assert.NotNull(Store.Popup.ActiveTriggerElement);
        Assert.True(Store.Popup.Open);
        Assert.Empty(requests);

        // The now-registered active trigger genuinely unmounts: the popup closes.
        await cut.Instance.SetAsync("triggers", Array.Empty<string>());

        (bool open, EventReason reason) = Assert.Single(requests);
        Assert.False(open);
        Assert.Equal(EventReason.None, reason);
        Assert.False(Store.Popup.Open);
        Assert.Null(Store.Popup.ActiveTriggerId);
        Assert.Null(Store.Popup.ActiveTriggerElement);
    }

    [Theory(DisplayName = "keeps the popup open when ownership returns to a pending trigger after %s")]
    [InlineData("a different unresolved id", false)]
    [InlineData("a different unresolved id", true)]
    [InlineData("no active trigger", false)]
    [InlineData("no active trigger", true)]
    [Trait("Requirement", "PSH-STA-24")]
    public async Task Keeps_the_popup_open_when_ownership_returns_to_a_pending_trigger_after(string description, bool withPopupPart)
    {
        string? pendingTriggerId = description == "a different unresolved id" ? "second" : null;
        List<bool> requests = [];
        IRenderedComponent<PopupTestHost> cut = RenderTree(triggers: ["first"], closeOnActiveTriggerUnmount: true, withPopupPart: withPopupPart, onOpenChange: args => requests.Add(args.Value));
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
    public async Task Keeps_the_popup_open_when_the_active_trigger_is_replaced_with_the_same_id(bool withPopupPart)
    {
        List<bool> requests = [];
        IRenderedComponent<PopupTestHost> cut = RenderTree(triggers: ["first", "second"], closeOnActiveTriggerUnmount: true, withPopupPart: withPopupPart, onOpenChange: args => requests.Add(args.Value));
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
    public async Task Keeps_the_popup_open_when_the_active_trigger_element_is_registered_with_another_id(bool withPopupPart)
    {
        List<bool> requests = [];
        IRenderedComponent<PopupTestHost> cut = RenderTree(triggers: ["registered-id"], closeOnActiveTriggerUnmount: true, withPopupPart: withPopupPart, onOpenChange: args => requests.Add(args.Value));
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
    public async Task Reassociates_the_active_trigger_when_ownership_moves_to_another_rendered_id_trigger_while_open(bool withPopupPart)
    {
        List<bool> requests = [];
        IRenderedComponent<PopupTestHost> cut = RenderTree(triggers: ["registered-1", "registered-2"], closeOnActiveTriggerUnmount: true, withPopupPart: withPopupPart, onOpenChange: args => requests.Add(args.Value));
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
    public async Task Preserves_active_trigger_ownership_without_closing_by_default(bool withPopupPart)
    {
        List<bool> requests = [];
        IRenderedComponent<PopupTestHost> cut = RenderTree(triggers: ["first", "second"], withPopupPart: withPopupPart, onOpenChange: args => requests.Add(args.Value));
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
    public async Task Resets_triggerCount_when_the_popup_closes(bool withPopupPart)
    {
        IRenderedComponent<PopupTestHost> cut = RenderTree(triggers: ["trigger"], defaultOpen: true, withPopupPart: withPopupPart);

        Assert.Equal(1, Store.Popup.TriggerCount);

        await cut.InvokeAsync(() => Store.WritePopup(Store.Popup with { Open = false }));

        Assert.Equal(0, Store.Popup.TriggerCount);
    }

    // The popupId selector block: the floating id is the Root's generated id (PSH-STA-01), drawn at its first parameter set;
    // the popup part reports the id it renders.
    [Fact(DisplayName = "syncs the floating id into the popup store for trigger ownership selectors")]
    [Trait("Requirement", "PSH-STA-04")]
    [Trait("Requirement", "PSH-STA-46")]
    public void Syncs_the_floating_id_into_the_popup_store_for_trigger_ownership_selectors()
    {
        RenderTree(triggers: [], withPopupPart: false);

        Assert.StartsWith("base-ui-", Store.Popup.FloatingId, StringComparison.Ordinal);
        Assert.Equal(Store.Popup.FloatingId, PopupSelectors<TestPopupStoreState<string>, string>.PopupId(Store.State));
    }

    [Fact(DisplayName = "omits popup id when the floating id is empty")]
    [Trait("Requirement", "PSH-STA-04")]
    public async Task Omits_popup_id_when_the_floating_id_is_empty()
    {
        IRenderedComponent<PopupTestHost> cut = RenderTree(triggers: [], withPopupPart: false);

        await cut.InvokeAsync(() => Store.WritePopup(Store.Popup with { FloatingId = string.Empty }));

        Assert.Equal(string.Empty, Store.Popup.FloatingId);
        Assert.Null(PopupSelectors<TestPopupStoreState<string>, string>.PopupId(Store.State));
    }

    [Fact(DisplayName = "prefers an explicit popup element id over the generated floating id")]
    [Trait("Requirement", "PSH-STA-04")]
    public void Prefers_an_explicit_popup_element_id_over_the_generated_floating_id()
    {
        test.Render((builder, _) =>
        {
            builder.OpenComponent<TestPopupRoot<string>>(0);
            builder.AddComponentParameter(1, nameof(TestPopupRoot<string>.DefaultOpen), true);
            builder.AddComponentParameter(2, nameof(TestPopupRoot<string>.ChildContent), (RenderFragment)(content =>
            {
                content.OpenComponent<TestPopupPopup<string>>(0);
                content.AddComponentParameter(1, "id", "explicit-popup-id");
                content.CloseComponent();
            }));
            builder.AddComponentReferenceCapture(3, reference => root = (TestPopupRoot<string>)reference);
            builder.CloseComponent();
        });

        Assert.NotEqual("explicit-popup-id", Store.Popup.FloatingId);
        Assert.Equal("explicit-popup-id", PopupSelectors<TestPopupStoreState<string>, string>.PopupId(Store.State));
    }

    [Fact(DisplayName = "associates a lone trigger with the popup id unless the popup opened without a trigger")]
    [Trait("Requirement", "PSH-STA-10")]
    public void Associates_a_lone_trigger_with_the_popup_id_unless_the_popup_opened_without_a_trigger()
    {
        // Base UI writes a bare store, with no root to reconcile the count against the registry.
        TestPopupStore<string> store = new("popup-id", sessionToken: null);

        store.WritePopup(store.Popup with { Open = true, TriggerCount = 1 });
        Assert.Equal("popup-id", PopupSelectors<TestPopupStoreState<string>, string>.TriggerPopupId(store.State, "trigger"));

        store.WritePopup(store.Popup with { OpenedWithoutTrigger = true });
        Assert.Null(PopupSelectors<TestPopupStoreState<string>, string>.TriggerPopupId(store.State, "trigger"));
    }

    // ADR 16 Amendments 2026-10-27 (design point 8): the claim that a family never re-renders its content for an open or a
    // close, measured; and the fallback's cost when no popup part is registered.
    [Fact(DisplayName = "port: with a popup part registered, an open and a close render neither the root nor an inactive trigger, and without one the store renders the root on the reconciliation's inputs")]
    [Trait("Requirement", "PSH-STA-16")]
    [Trait("Requirement", "PSH-STA-18")]
    [Trait("Requirement", "ADR16-reconciliation-boundary")]
    public async Task With_a_popup_part_an_open_and_a_close_render_neither_the_root_nor_an_inactive_trigger()
    {
        Dictionary<string, int> renders = [];
        IRenderedComponent<PopupTestHost> cut = RenderTree(triggers: ["a", "b"], renders: renders);
        TestPopupRoot<string> familyRoot = root!;
        int rootRenders = familyRoot.Renders;
        int inactiveRenders = renders["b"];
        Assert.True(Store.HasBoundaryPart);

        await cut.Find("#a").ClickAsync(new());
        Assert.True(Store.IsOpen);
        Assert.Equal("a", Store.Popup.ActiveTriggerId);
        await cut.Find("#a").ClickAsync(new());
        Assert.False(Store.IsOpen);

        Assert.Equal(rootRenders, familyRoot.Renders);
        Assert.Equal(inactiveRenders, renders["b"]);

        Dictionary<string, int> bareRenders = [];
        IRenderedComponent<PopupTestHost> bare = RenderTree(triggers: ["c", "d"], withPopupPart: false, renders: bareRenders);
        rootRenders = root!.Renders;
        inactiveRenders = bareRenders["d"];
        Assert.False(Store.HasBoundaryPart);

        await bare.Find("#c").ClickAsync(new());

        Assert.Equal("c", Store.Popup.ActiveTriggerId);
        Assert.Equal(rootRenders + 1, root.Renders);
        Assert.Equal(inactiveRenders + 1, bareRenders["d"]);
    }

    // Renders a root of string payloads around the triggers the host's "triggers" value names (null for a trigger without an
    // id); "ids" renames a trigger's id, "keys" gives one a new instance under the same id, "payloads" gives one a payload.
    private IRenderedComponent<PopupTestHost> RenderTree(
        string?[] triggers,
        bool defaultOpen = false,
        bool closeOnActiveTriggerUnmount = false,
        bool withPopupPart = true,
        Action<ValueChangeEventArgs<bool>>? onOpenChange = null,
        Dictionary<string, int>? renders = null,
        bool hideTriggersAfterFirstRender = false)
    {
        IRenderedComponent<PopupTestHost> cut = test.Render((builder, host) =>
        {
            builder.OpenComponent<TestPopupRoot<string>>(0);
            builder.AddComponentParameter(1, nameof(TestPopupRoot<string>.DefaultOpen), defaultOpen);
            builder.AddComponentParameter(2, nameof(TestPopupRoot<string>.CloseOnActiveTriggerUnmount), closeOnActiveTriggerUnmount);
            if (onOpenChange is not null)
            {
                builder.AddComponentParameter(4, nameof(TestPopupRoot<string>.OnOpenChange), EventCallback.Factory.Create(this, onOpenChange));
            }

            builder.AddComponentParameter(5, nameof(TestPopupRoot<string>.ChildContent), (RenderFragment)(content => RenderTriggers(content, host, withPopupPart, renders)));
            builder.AddComponentReferenceCapture(6, reference => root = (TestPopupRoot<string>)reference);
            builder.CloseComponent();
            if (hideTriggersAfterFirstRender)
            {
                builder.OpenComponent<AfterFirstRender>(7);
                builder.AddComponentParameter(8, nameof(AfterFirstRender.Run), (Func<Task>)(() => host.SetAsync("triggers", Array.Empty<string>())));
                builder.CloseComponent();
            }
        }, new() { ["triggers"] = triggers });
        return cut;
    }

    private static void RenderTriggers(RenderTreeBuilder builder, PopupTestHost host, bool withPopupPart, Dictionary<string, int>? renders)
    {
        string?[] triggers = host.Get<string?[]>("triggers") ?? [];
        Dictionary<string, string> ids = host.Get<Dictionary<string, string>>("ids") ?? [];
        Dictionary<string, string> keys = host.Get<Dictionary<string, string>>("keys") ?? [];
        Dictionary<string, string> payloads = host.Get<Dictionary<string, string>>("payloads") ?? [];
        // Every family renders a popup part that selects the open and mounted state (its Portal and Popup), which brings the
        // render-batch boundary after an open that no trigger's state shows; without one, the store renders the root on the
        // reconciliation's inputs (design point 8, ADR 16 Amendments 2026-10-27).
        if (withPopupPart)
        {
            builder.OpenComponent<TestPopupPopup<string>>(4);
            builder.CloseComponent();
        }
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
            if (renders is not null && name is not null)
            {
                // Render counts are measured through ClassValue, which the pipeline calls once per render of the part.
                builder.AddComponentParameter(5, nameof(TestPopupTrigger<string>.ClassValue), (Func<TestPopupTriggerState, string?>)(_ =>
                {
                    renders[name] = renders.GetValueOrDefault(name) + 1;
                    return null;
                }));
            }

            builder.CloseComponent();
        }
    }
}

/// <summary>Runs a callback once, after its first render: Base UI's <c>HideOnLayout</c>, at the batch boundary.</summary>
internal sealed class AfterFirstRender : ComponentBase
{
    [Parameter]
    public Func<Task>? Run { get; set; }

    protected override Task OnAfterRenderAsync(bool firstRender) => firstRender && Run is not null ? Run() : Task.CompletedTask;
}
