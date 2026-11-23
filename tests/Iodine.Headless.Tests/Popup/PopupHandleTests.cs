using Blazix.Primitives.Tests.TestSupport;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests;

/// <summary>
/// The popup handle at L1 (utils/popups/popupHandle.test.ts, titles verbatim; PSH-API-01/02/04/05, PSH-STA-34..38,
/// PSH-EVT-06, PSH-DIS-03, PSH-EDGE-02..05/14). The handle points at <see cref="FakePopupStore{TPayload}"/>s, Base UI's
/// FakeStore. The overlap warning's three titles are L2 (PSH-EDGE-06 at the render-batch boundary). Diagnostics follow
/// design point 6 of M4 track 1 (settled by the orchestrator 2026-10-27): a handle writes through the logger of the root it
/// last attached to, so a warning raised before any root attached is dropped (the dated note on PSH-EDGE-02), and the two
/// titles that warn before a first attach attach and detach a root first.
/// </summary>
public sealed class PopupHandleTests
{
    private const string DetachedWarning = "no root using this handle is mounted";

    private static PopupTriggerElement RegisterTrigger(IPopupStore<NoPayload> store, string id)
    {
        PopupTriggerElement element = new(new object(), new Microsoft.AspNetCore.Components.ElementReference(id + "-element"));
        store.Triggers.Add(id, element);
        return element;
    }

    private static (bool Open, OpenChangeEventDetails Details) SingleRequest(FakePopupStore<NoPayload> store) => Assert.Single(store.Requests);

    [Fact(DisplayName = "exposes the fallback store until a root store attaches, and again after it detaches")]
    [Trait("Requirement", "PSH-STA-34")]
    [Trait("Requirement", "PSH-API-01")]
    public void Exposes_the_fallback_store_until_a_root_store_attaches_and_again_after_it_detaches()
    {
        FakeStoreHandle<NoPayload> handle = new();
        FakePopupStore<NoPayload> rootStore = new();

        Assert.Same(handle.Fallback, handle.Store);
        Assert.Same(handle.Fallback, handle.ServerStore);

        IDisposable detach = handle.AttachStore(rootStore);
        Assert.Same(rootStore, handle.Store);
        // The server snapshot stays on the fallback store so prerendering is stable.
        Assert.Same(handle.Fallback, handle.ServerStore);

        detach.Dispose();
        Assert.Same(handle.Fallback, handle.Store);
        Assert.NotSame(handle.Fallback, new FakeStoreHandle<NoPayload>().Fallback);
    }

    [Fact(DisplayName = "notifies subscribers only when the exposed store changes")]
    [Trait("Requirement", "PSH-STA-36")]
    public void Notifies_subscribers_only_when_the_exposed_store_changes()
    {
        FakeStoreHandle<NoPayload> handle = new();
        FakePopupStore<NoPayload> storeA = new();
        FakePopupStore<NoPayload> storeB = new();
        int calls = 0;
        IDisposable unsubscribe = handle.SubscribeStore(() => calls++);

        IDisposable detachA = handle.AttachStore(storeA);
        Assert.Equal(1, calls);

        IDisposable detachB = handle.AttachStore(storeB);
        Assert.Equal(2, calls);

        // Detaching a root that is not the active one leaves the exposed store unchanged.
        detachA.Dispose();
        Assert.Same(storeB, handle.Store);
        Assert.Equal(2, calls);

        detachB.Dispose();
        Assert.Same(handle.Fallback, handle.Store);
        Assert.Equal(3, calls);

        unsubscribe.Dispose();
        handle.AttachStore(storeA);
        Assert.Equal(3, calls);
    }

    [Fact(DisplayName = "restores control to the most recently attached root that is still attached")]
    [Trait("Requirement", "PSH-STA-35")]
    public void Restores_control_to_the_most_recently_attached_root_that_is_still_attached()
    {
        FakeStoreHandle<NoPayload> handle = new();
        FakePopupStore<NoPayload> storeA = new();
        FakePopupStore<NoPayload> storeB = new();
        FakePopupStore<NoPayload> storeC = new();

        IDisposable detachA = handle.AttachStore(storeA);
        IDisposable detachB = handle.AttachStore(storeB);
        IDisposable detachC = handle.AttachStore(storeC);
        Assert.Same(storeC, handle.Store);

        // The newest root detaches first (a canceled route transition).
        detachC.Dispose();
        Assert.Same(storeB, handle.Store);

        detachA.Dispose();
        Assert.Same(storeB, handle.Store);

        detachB.Dispose();
        Assert.Same(handle.Fallback, handle.Store);
    }

    [Fact(DisplayName = "stays attached when the same root store detaches and reattaches (Strict Mode replay)")]
    [Trait("Requirement", "PSH-STA-35")]
    [Trait("Requirement", "PSH-EDGE-14")]
    public async Task Stays_attached_when_the_same_root_store_detaches_and_reattaches()
    {
        FakeStoreHandle<NoPayload> handle = new();
        FakePopupStore<NoPayload> rootStore = new();
        int calls = 0;
        handle.SubscribeStore(() => calls++);

        handle.AttachStore(rootStore).Dispose();
        handle.AttachStore(rootStore);

        Assert.Same(rootStore, handle.Store);
        // Notified on both steps: the exposed store passes through the fallback in between.
        Assert.Equal(3, calls);
        await handle.CloseAsync();
        Assert.False(SingleRequest(rootStore).Open);
    }

    [Fact(DisplayName = "ignores open() and close() with a warning before a root attaches and after it detaches")]
    [Trait("Requirement", "PSH-EDGE-02")]
    [Trait("Requirement", "PSH-STA-37")]
    public async Task Ignores_open_and_close_with_a_warning_before_a_root_attaches_and_after_it_detaches()
    {
        CapturingLogger logger = new();
        FakeStoreHandle<NoPayload> handle = new();
        FakePopupStore<NoPayload> rootStore = new(logger);
        RegisterTrigger(handle.Fallback, "trigger");

        // Before any root attached the handle has no logger: the two warnings are dropped (design point 6).
        await handle.OpenAsync("trigger");
        await handle.CloseAsync();
        Assert.Empty(logger.Messages);
        Assert.Empty(handle.Fallback.Requests);

        handle.AttachStore(rootStore).Dispose();

        await handle.OpenAsync("trigger");
        await handle.CloseAsync();
        Assert.Equal(
            [
                "Blazix: `TestHandle.OpenAsync()` was called while no root using this handle is mounted. The call was ignored; mount a root with this handle before opening it imperatively.",
                "Blazix: `TestHandle.CloseAsync()` was called while no root using this handle is mounted. The call was ignored.",
            ],
            logger.Messages);
        Assert.Equal(2, logger.Messages.Count(message => message.Contains(DetachedWarning, StringComparison.Ordinal)));
        Assert.Empty(rootStore.Requests);
        Assert.Empty(handle.Fallback.Requests);
    }

    [Fact(DisplayName = "opens the attached root with the trigger registered in its store")]
    [Trait("Requirement", "PSH-EVT-06")]
    public async Task Opens_the_attached_root_with_the_trigger_registered_in_its_store()
    {
        FakeStoreHandle<NoPayload> handle = new();
        FakePopupStore<NoPayload> rootStore = new();
        handle.AttachStore(rootStore);
        RegisterTrigger(rootStore, "other");
        PopupTriggerElement trigger = RegisterTrigger(rootStore, "trigger");

        await handle.OpenAsync("trigger");

        (bool open, OpenChangeEventDetails details) = SingleRequest(rootStore);
        Assert.True(open);
        Assert.Equal(EventReason.ImperativeAction, details.Reason);
        Assert.Equal("trigger", details.Trigger?.Id);
        Assert.Equal(trigger.Element?.Id, details.Trigger?.Element?.Id);
    }

    [Theory(DisplayName = "resolves a trigger that is still registered in $location")]
    [InlineData("the fallback store")]
    [InlineData("a previously attached root store")]
    [Trait("Requirement", "PSH-STA-38")]
    public async Task Resolves_a_trigger_that_is_still_registered_in_location(string location)
    {
        FakeStoreHandle<NoPayload> handle = new();
        FakePopupStore<NoPayload> previousStore = new();
        FakePopupStore<NoPayload> activeStore = new();
        handle.AttachStore(previousStore);
        handle.AttachStore(activeStore);

        PopupTriggerElement trigger = RegisterTrigger(location == "the fallback store" ? handle.Fallback : previousStore, "trigger");

        await handle.OpenAsync("trigger");

        Assert.Empty(previousStore.Requests);
        Assert.Equal(trigger.Element?.Id, SingleRequest(activeStore).Details.Trigger?.Element?.Id);
    }

    [Fact(DisplayName = "prefers the newest attached store when several register the same id")]
    [Trait("Requirement", "PSH-STA-38")]
    public async Task Prefers_the_newest_attached_store_when_several_register_the_same_id()
    {
        FakeStoreHandle<NoPayload> handle = new();
        FakePopupStore<NoPayload> previousStore = new();
        FakePopupStore<NoPayload> activeStore = new();
        handle.AttachStore(previousStore);
        handle.AttachStore(activeStore);
        handle.Fallback.Triggers.Add("trigger", new PopupTriggerElement(new object(), new Microsoft.AspNetCore.Components.ElementReference("fallback")));
        previousStore.Triggers.Add("trigger", new PopupTriggerElement(new object(), new Microsoft.AspNetCore.Components.ElementReference("previous")));
        activeStore.Triggers.Add("trigger", new PopupTriggerElement(new object(), new Microsoft.AspNetCore.Components.ElementReference("active")));

        await handle.OpenAsync("trigger");

        Assert.Equal("active", SingleRequest(activeStore).Details.Trigger?.Element?.Id);
    }

    [Theory(DisplayName = "opens without a trigger when the id is $triggerId")]
    [InlineData(null)]
    [InlineData("")]
    [Trait("Requirement", "PSH-API-02")]
    [Trait("Requirement", "PSH-EDGE-05")]
    public async Task Opens_without_a_trigger_when_the_id_is_triggerId(string? triggerId)
    {
        CapturingLogger logger = new();
        FakeStoreHandle<NoPayload> handle = new();
        FakePopupStore<NoPayload> rootStore = new(logger);
        handle.AttachStore(rootStore);

        await handle.OpenAsync(triggerId);

        (bool open, OpenChangeEventDetails details) = SingleRequest(rootStore);
        Assert.True(open);
        Assert.Null(details.Trigger);
        Assert.Empty(logger.Messages);
    }

    [Fact(DisplayName = "throws for an unregistered trigger id by default and does not open")]
    [Trait("Requirement", "PSH-EDGE-03")]
    public async Task Throws_for_an_unregistered_trigger_id_by_default_and_does_not_open()
    {
        FakeStoreHandle<NoPayload> handle = new();
        FakePopupStore<NoPayload> rootStore = new();
        handle.AttachStore(rootStore);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() => handle.OpenAsync("missing"));

        Assert.Equal(
            "Blazix: `TestHandle.OpenAsync()` was called with the trigger id \"missing\", but no matching trigger is registered with this handle. An anchored popup cannot open without a trigger to anchor to. Pass the id of a mounted `TestTrigger` that has this handle set on its `Handle` parameter.",
            exception.Message);
        Assert.Empty(rootStore.Requests);
    }

    [Fact(DisplayName = "opens unassociated with a warning for an unregistered trigger id when throwing is disabled")]
    [Trait("Requirement", "PSH-EDGE-04")]
    public async Task Opens_unassociated_with_a_warning_for_an_unregistered_trigger_id_when_throwing_is_disabled()
    {
        CapturingLogger logger = new();
        FakeStoreHandle<NoPayload> handle = new(throwOnMissingTrigger: false);
        FakePopupStore<NoPayload> rootStore = new(logger);
        handle.AttachStore(rootStore);

        await handle.OpenAsync("missing");

        Assert.Equal(
            ["Blazix: `TestHandle.OpenAsync()`: No trigger found with id \"missing\". The popup will open, but the trigger will not be associated with it."],
            logger.Messages);
        (bool open, OpenChangeEventDetails details) = SingleRequest(rootStore);
        Assert.True(open);
        Assert.Null(details.Trigger);
    }

    [Fact(DisplayName = "closes the attached root")]
    [Trait("Requirement", "PSH-API-04")]
    [Trait("Requirement", "PSH-DIS-03")]
    public async Task Closes_the_attached_root()
    {
        FakeStoreHandle<NoPayload> handle = new();
        FakePopupStore<NoPayload> rootStore = new();
        handle.AttachStore(rootStore);

        await handle.CloseAsync();

        (bool open, OpenChangeEventDetails details) = SingleRequest(rootStore);
        Assert.False(open);
        Assert.Equal(EventReason.ImperativeAction, details.Reason);
        Assert.Null(details.Trigger);
    }

    // The concrete family handles run this title per family (tracks 5 to 9); here it runs on the test-only handle. The
    // handle names itself after a root attached and detached, since a warning before the first attach is dropped.
    [Fact(DisplayName = "$component handle is closed and names itself in warnings while no root is attached")]
    [Trait("Requirement", "PSH-API-05")]
    [Trait("Requirement", "PSH-EDGE-02")]
    public async Task Component_handle_is_closed_and_names_itself_in_warnings_while_no_root_is_attached()
    {
        CapturingLogger logger = new();
        FakeStoreHandle<NoPayload> handle = new(componentName: "Popover");
        handle.AttachStore(new FakePopupStore<NoPayload>(logger)).Dispose();

        await handle.OpenAsync("trigger");
        await handle.CloseAsync();

        Assert.False(handle.IsOpen);
        Assert.Equal(1, logger.Messages.Count(message => message.Contains("`PopoverHandle.OpenAsync()` was called while", StringComparison.Ordinal)));
        Assert.Equal(1, logger.Messages.Count(message => message.Contains("`PopoverHandle.CloseAsync()` was called while", StringComparison.Ordinal)));
    }

    [Fact(DisplayName = "$component handle ignores openWithPayload() with a warning while detached")]
    [Trait("Requirement", "PSH-API-03")]
    [Trait("Requirement", "PSH-EDGE-02")]
    public async Task Component_handle_ignores_openWithPayload_with_a_warning_while_detached()
    {
        CapturingLogger logger = new();
        FakeStoreHandle<int> handle = new(throwOnMissingTrigger: false, componentName: "Dialog");
        FakePopupStore<int> rootStore = new(logger);
        handle.AttachStore(rootStore).Dispose();

        await handle.OpenWithPayloadCoreAsync(8);

        Assert.False(handle.IsOpen);
        Assert.Empty(rootStore.Payloads);
        Assert.Equal(
            ["Blazix: `DialogHandle.OpenWithPayloadAsync()` was called while no root using this handle is mounted. The call and its payload were ignored; mount a root with this handle before opening it imperatively."],
            logger.Messages);
    }

    // PSH-STA-39: the payload, then a trigger-less open with reason imperative-action (the Dialog family's
    // OpenWithPayloadAsync, track 8, calls this core).
    [Fact(DisplayName = "port: openWithPayload writes the payload, then opens without a trigger")]
    [Trait("Requirement", "PSH-STA-39")]
    public async Task OpenWithPayload_writes_the_payload_then_opens_without_a_trigger()
    {
        FakeStoreHandle<int> handle = new(throwOnMissingTrigger: false, componentName: "Dialog");
        FakePopupStore<int> rootStore = new();
        handle.AttachStore(rootStore);

        await handle.OpenWithPayloadCoreAsync(8);

        Assert.Equal([8], rootStore.Payloads);
        (bool open, OpenChangeEventDetails details) = Assert.Single(rootStore.Requests);
        Assert.True(open);
        Assert.Equal(EventReason.ImperativeAction, details.Reason);
        Assert.Null(details.Trigger);
    }

    // PSH-STR-02: a handle of another popup family cannot serve a trigger; the message names the part as written, with no
    // type-parameter suffix.
    [Fact(DisplayName = "port: a trigger given another popup family's handle throws, naming the part")]
    [Trait("Requirement", "PSH-STR-02")]
    public void A_trigger_given_another_popup_familys_handle_throws_naming_the_part()
    {
        FakeStoreHandle<NoPayload> handle = new();
        using PopupTriggerBehavior<TestFamilyStore<NoPayload>, TestFamilyStoreState<NoPayload>, NoPayload> behavior = new(new object(), "TestTrigger", () => { });

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => behavior.Apply(null, handle, "trigger", null, false, default, "missing root"));

        Assert.Equal("Blazix: the `Handle` of `TestTrigger` belongs to another popup family.", error.Message);
    }
}
