using System.Text.Json;
using AngleSharp.Dom;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The <c>[L2]</c> titles of <c>DialogRoot.detached-triggers.test.tsx</c> outside its conformance suite: inactive triggers
/// that do not render (DLG-EDGE-16, ANI-06, ANI-09, ARIA-02), the nested state of handle-backed dialogs (DLG-STA-05..07,
/// DOM-19, DOM-22, ANI-10, KEY-04, PTR-12, DIS-03), handle-backed root ownership (DLG-EDGE-04/05/07/11/13, API-08, API-24..27,
/// DIS-02), payloads (DLG-API-09/13/25, STA-12, STA-14, DOM-03, ARIA-11, DIS-08) and remounts (DLG-EDGE-06, EDGE-12,
/// ARIA-03). Titles are Base UI's; a <c>port:</c> title restates one bUnit cannot run as written: the hydration titles over
/// a trigger rendered before its root attaches (no prerender), the fake-timer titles over the handle's overlap check at the
/// attaching root's next render batch (ADR 01 Amendments 2026-10-27), Strict Mode as a root that renders again. A press is
/// the <c>interactions</c> module's report, driven into the trigger's twin; a close's exit never ends here (no
/// <c>transitions</c> module), so a title that waits for the popup to go ends the exit with the root's <c>Unmount()</c>.
/// </summary>
public sealed class DialogDetachedTriggersTests : IDisposable
{
    private const string DetachedWarning = "no root using this handle is mounted";
    private const string OverlapWarning = "more than one mounted root";
    private const string OpenWithPayloadWarning = "Blazix: `DialogHandle.OpenWithPayloadAsync()` was called while no root using this handle is mounted. The call and its payload were ignored; mount a root with this handle before opening it imperatively.";

    private readonly DialogTestContext test = new();
    private readonly CapturingLogger<DialogRoot<int?>> numberLogger = new();

    public DialogDetachedTriggersTests()
    {
        test.Context.Services.AddSingleton<ILogger<DialogRoot<int?>>>(numberLogger);
    }

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "when the popup opens and closes")]
    [Trait("Requirement", "DLG-ANI-06")]
    [Trait("Requirement", "DLG-ANI-09")]
    [Trait("Requirement", "DLG-EDGE-16")]
    public async Task When_the_popup_opens_and_closes()
    {
        int[] renders = new int[3];
        IRenderedComponent<DialogInactiveTriggersHarness> cut = RenderInactiveTriggers(renders);

        for (int round = 0; round < 2; round++)
        {
            await test.PressAsync(cut, "trigger-1", open: true);
            ExpectOpenedBy(cut, "trigger-1");
            await test.EscapeAsync(cut);
            await ExpectClosedAsync(cut, "trigger-1");
        }

        // The active trigger's count moved, so the counters count; the inactive ones' did not.
        Assert.NotEqual(0, renders[0]);
        Assert.Equal(0, renders[1]);
        Assert.Equal(0, renders[2]);
    }

    [Fact(DisplayName = "when the popup moves to another trigger")]
    [Trait("Requirement", "DLG-ARIA-02")]
    [Trait("Requirement", "DLG-ANI-09")]
    [Trait("Requirement", "DLG-EDGE-16")]
    public async Task When_the_popup_moves_to_another_trigger()
    {
        int[] renders = new int[3];
        IRenderedComponent<DialogInactiveTriggersHarness> cut = RenderInactiveTriggers(renders);
        DialogHandle<NoPayload> handle = cut.Instance.Handle;

        await cut.InvokeAsync(() => handle.OpenAsync("trigger-1"));
        ExpectOpenedBy(cut, "trigger-1");
        await cut.InvokeAsync(() => handle.OpenAsync("trigger-2"));
        ExpectOpenedBy(cut, "trigger-2");
        Assert.Equal("false", cut.Find("#trigger-1").GetAttribute("aria-expanded"));
        await cut.InvokeAsync(() => handle.CloseAsync());
        await ExpectClosedAsync(cut, "trigger-2");

        // The triggers the popup moved between rendered, so the counters count; the bystander did not.
        Assert.NotEqual(0, renders[1]);
        Assert.Equal(0, renders[2]);
    }

    [Fact(DisplayName = "opens a handle-backed dialog as topmost over a dialog that opened while it was closed")]
    [Trait("Requirement", "DLG-STA-06")]
    [Trait("Requirement", "DLG-KEY-04")]
    [Trait("Requirement", "DLG-DIS-03")]
    public async Task Opens_a_handle_backed_dialog_as_topmost_over_a_dialog_that_opened_while_it_was_closed()
    {
        IRenderedComponent<DialogHandleNestingHarness> cut = test.Context.Render<DialogHandleNestingHarness>(p => p.Add(x => x.Shape, "topmost"));

        await test.PressAsync(cut, "section-trigger", open: true);
        Assert.Single(cut.FindAll("[data-testid=section]"));

        await OpenHelpAsync();

        // Escape reaches the topmost session, the help's: the module routes it, and L2 drives that session's decision.
        string helpId = cut.Find("[data-testid=help]").Id!;
        await cut.InvokeAsync(() => SessionTwinOf(helpId).Decide(InteractionsTwin.EscapePoint, DialogTestContext.EscapeJson));
        Assert.True(cut.Find("[data-testid=help]").HasAttribute("data-closed"));
        await cut.InvokeAsync(() => cut.Instance.HelpRoot!.Unmount());
        Assert.Empty(cut.FindAll("[data-testid=help]"));
        Assert.True(cut.Find("[data-testid=section]").HasAttribute("data-open"));

        await OpenHelpAsync();

        // The press on the help's backdrop that closes the help and keeps the section open needs a real browser: the same
        // title at L4.

        async Task OpenHelpAsync()
        {
            await test.PressAsync(cut, "help-trigger", open: true);
            IElement help = cut.Find("[data-testid=help]");
            Assert.False(help.HasAttribute("data-nested-dialog-open"));
            Assert.Equal("0", NestedDialogs(help));

            // Topmost: the help's session leaves Escape and outside presses on.
            JsonElement dismiss = LastDismiss(help.Id!);
            Assert.False(dismiss.TryGetProperty("escapeKey", out _));
            Assert.False(dismiss.TryGetProperty("outsidePress", out _));
        }
    }

    [Fact(DisplayName = "resets nested state recorded while open when a handle-backed dialog closes")]
    [Trait("Requirement", "DLG-STA-06")]
    [Trait("Requirement", "DLG-DOM-22")]
    [Trait("Requirement", "DLG-ANI-10")]
    public async Task Resets_nested_state_recorded_while_open_when_a_handle_backed_dialog_closes()
    {
        IRenderedComponent<DialogHandleNestingHarness> cut = test.Context.Render<DialogHandleNestingHarness>(p => p.Add(x => x.Shape, "reset"));
        DialogHandle<NoPayload> help = cut.Instance.Help;

        await cut.InvokeAsync(() => help.OpenAsync(null));
        await cut.InvokeAsync(() => cut.Instance.Section.OpenAsync(null));
        Assert.Single(cut.FindAll("[data-testid=section]"));
        Assert.True(cut.Find("[data-testid=help]").HasAttribute("data-nested-dialog-open"));

        // The section dialog stays open, so it never sends a zeroed count.
        await cut.InvokeAsync(() => help.CloseAsync());
        await cut.InvokeAsync(() => cut.Instance.HelpRoot!.Unmount());
        Assert.Empty(cut.FindAll("[data-testid=help]"));
        Assert.Single(cut.FindAll("[data-testid=section]"));

        await cut.InvokeAsync(() => help.OpenAsync(null));
        IElement reopened = cut.Find("[data-testid=help]");
        Assert.False(reopened.HasAttribute("data-nested-dialog-open"));
        Assert.Equal("0", NestedDialogs(reopened));
    }

    [Fact(DisplayName = "updates the parent nested state each time a handle-backed nested dialog opens and closes")]
    [Trait("Requirement", "DLG-STA-05")]
    [Trait("Requirement", "DLG-DOM-19")]
    public async Task Updates_the_parent_nested_state_each_time_a_handle_backed_nested_dialog_opens_and_closes()
    {
        IRenderedComponent<DialogHandleNestingHarness> cut = test.Context.Render<DialogHandleNestingHarness>(p => p.Add(x => x.Shape, "child"));
        Assert.Single(cut.FindAll("[data-testid=parent]"));

        for (int round = 0; round < 2; round++)
        {
            await test.PressAsync(cut, "child-trigger", open: true);
            Assert.Single(cut.FindAll("[data-testid=child]"));
            Assert.True(cut.Find("[data-testid=parent]").HasAttribute("data-nested-dialog-open"));
            Assert.Equal("1", NestedDialogs(cut.Find("[data-testid=parent]")));

            await cut.Find("[data-testid=close-child]").ClickAsync(new MouseEventArgs { Detail = 1 });
            await cut.InvokeAsync(() => cut.Instance.ChildRoot!.Unmount());
            Assert.Empty(cut.FindAll("[data-testid=child]"));
            Assert.False(cut.Find("[data-testid=parent]").HasAttribute("data-nested-dialog-open"));
            Assert.Equal("0", NestedDialogs(cut.Find("[data-testid=parent]")));
        }
    }

    [Fact(DisplayName = "keeps the parent nested state when a closed handle-backed nested dialog mounts")]
    [Trait("Requirement", "DLG-STA-06")]
    [Trait("Requirement", "DLG-STA-07")]
    [Trait("Requirement", "DLG-PTR-12")]
    [Trait("Requirement", "DLG-DIS-03")]
    public void Keeps_the_parent_nested_state_when_a_closed_handle_backed_nested_dialog_mounts()
    {
        IRenderedComponent<DialogHandleNestingHarness> cut = test.Context.Render<DialogHandleNestingHarness>(p => p.Add(x => x.Shape, "siblings"));

        Assert.Single(cut.FindAll("[data-testid=nested]"));
        IElement parent = cut.Find("[data-testid=parent]");
        Assert.True(parent.HasAttribute("data-nested-dialog-open"));
        Assert.Equal("1", NestedDialogs(parent));

        // The parent isn't topmost while the nested dialog is open: its session ignores Escape and outside presses. That a
        // press on its backdrop does not dismiss it needs a real browser: the same title at L4.
        JsonElement dismiss = LastDismiss(parent.Id!);
        Assert.False(dismiss.GetProperty("escapeKey").GetBoolean());
        Assert.False(dismiss.GetProperty("outsidePress").GetBoolean());
        Assert.Empty(cut.Instance.ParentOpenChanges);
        Assert.True(parent.HasAttribute("data-open"));
    }

    // DLG-EDGE-13 restated: Base UI's server render reads the handle's inert fallback store; bUnit has no prerender, so the
    // trigger renders while its root is not mounted, then the root mounts open with the trigger as its default trigger.
    [Fact(DisplayName = "port: renders a detached trigger from the handle's fallback store until its root attaches")]
    [Trait("Requirement", "DLG-DOM-03")]
    [Trait("Requirement", "DLG-ARIA-02")]
    [Trait("Requirement", "DLG-EDGE-13")]
    public void Renders_a_detached_trigger_from_the_handles_fallback_store_until_its_root_attaches()
    {
        IRenderedComponent<DialogFallbackStoreHarness> cut = test.Context.Render<DialogFallbackStoreHarness>(p => p.Add(x => x.RootMounted, false));

        IElement trigger = cut.Find("#trigger");
        Assert.Equal("false", trigger.GetAttribute("aria-expanded"));
        Assert.False(trigger.HasAttribute("data-popup-open"));

        cut.Render(p => p.Add(x => x.RootMounted, true));

        trigger = cut.Find("#trigger");
        Assert.Equal("true", trigger.GetAttribute("aria-expanded"));
        Assert.True(trigger.HasAttribute("data-popup-open"));
    }

    // DLG-EDGE-13 restated: the root mounts open with the trigger as its default trigger while the trigger's render is
    // delayed (Base UI's suspended hydration), and closes before the trigger renders.
    [Fact(DisplayName = "port: renders a delayed detached trigger closed when the root closes before the trigger renders")]
    [Trait("Requirement", "DLG-API-27")]
    [Trait("Requirement", "DLG-EDGE-13")]
    public async Task Renders_a_delayed_detached_trigger_closed_when_the_root_closes_before_the_trigger_renders()
    {
        TaskCompletionSource loading = new(TaskCreationOptions.RunContinuationsAsynchronously);
        IRenderedComponent<DialogFallbackStoreHarness> cut = test.Context.Render<DialogFallbackStoreHarness>(p => p.Add(x => x.TriggerLoading, loading.Task));
        DialogHandle<NoPayload> handle = cut.Instance.Handle;
        Assert.Empty(cut.FindAll("#trigger"));
        Assert.True(handle.IsOpen);

        await cut.InvokeAsync(() => handle.CloseAsync());
        Assert.False(handle.IsOpen);

        loading.SetResult();
        IElement trigger = cut.WaitForElement("#trigger");
        Assert.Equal("false", trigger.GetAttribute("aria-expanded"));
        Assert.False(trigger.HasAttribute("data-popup-open"));
    }

    // Base UI's layout effect is a descendant whose first OnAfterRenderAsync calls the handle, in the root's first batch.
    [Fact(DisplayName = "opens from a descendant layout effect on initial mount")]
    [Trait("Requirement", "DLG-EDGE-05")]
    public void Opens_from_a_descendant_layout_effect_on_initial_mount()
    {
        DialogHandle<NoPayload> handle = new();
        test.Context.Render<DialogMountEffectHarness>(p => p
            .Add(x => x.Handle, handle)
            .Add(x => x.Run, () => handle.OpenAsync(null)));

        Assert.DoesNotContain(test.RootLogger.Entries, entry => entry.Message.Contains(DetachedWarning, StringComparison.Ordinal));
        Assert.True(handle.IsOpen);
    }

    [Fact(DisplayName = "opens with a payload from a descendant layout effect on initial mount")]
    [Trait("Requirement", "DLG-API-25")]
    public void Opens_with_a_payload_from_a_descendant_layout_effect_on_initial_mount()
    {
        DialogHandle<int?> handle = new();
        IRenderedComponent<DialogMountEffectHarness> cut = test.Context.Render<DialogMountEffectHarness>(p => p
            .Add(x => x.NumberHandle, handle)
            .Add(x => x.Run, () => handle.OpenWithPayloadAsync(8)));

        Assert.DoesNotContain(numberLogger.Entries, entry => entry.Message.Contains(DetachedWarning, StringComparison.Ordinal));
        Assert.True(handle.IsOpen);
        Assert.Equal("8", cut.Find("[data-testid=payload]").TextContent);
    }

    [Fact(DisplayName = "closes from a descendant layout effect on an initially open root")]
    [Trait("Requirement", "DLG-API-26")]
    [Trait("Requirement", "DLG-EDGE-05")]
    public void Closes_from_a_descendant_layout_effect_on_an_initially_open_root()
    {
        DialogHandle<NoPayload> handle = new();
        test.Context.Render<DialogMountEffectHarness>(p => p
            .Add(x => x.Handle, handle)
            .Add(x => x.DefaultOpen, true)
            .Add(x => x.Run, () => handle.CloseAsync()));

        Assert.DoesNotContain(test.RootLogger.Entries, entry => entry.Message.Contains(DetachedWarning, StringComparison.Ordinal));
        Assert.False(handle.IsOpen);
    }

    [Fact(DisplayName = "ignores openWithPayload() after the root unmounts and keeps the remounted root without payload")]
    [Trait("Requirement", "DLG-API-25")]
    [Trait("Requirement", "DLG-EDGE-04")]
    public async Task Ignores_openWithPayload_after_the_root_unmounts_and_keeps_the_remounted_root_without_payload()
    {
        IRenderedComponent<DialogRemountHarness> cut = test.Context.Render<DialogRemountHarness>();
        DialogHandle<int?> handle = cut.Instance.Handle;
        cut.Render(p => p.Add(x => x.Mounted, false));

        await cut.InvokeAsync(() => handle.OpenWithPayloadAsync(8));
        (LogLevel _, string message) = Assert.Single(numberLogger.Entries, entry => entry.Message.Contains("OpenWithPayloadAsync()` was called while no root using this handle", StringComparison.Ordinal));
        Assert.Equal(OpenWithPayloadWarning, message);
        Assert.False(handle.IsOpen);

        cut.Render(p => p.Add(x => x.Mounted, true));
        Assert.False(handle.IsOpen);
        Assert.Equal("No payload", cut.Find("[data-testid=payload]").TextContent);
    }

    [Fact(DisplayName = "attaches a fresh root store when the handle prop changes to a previously dirtied handle")]
    [Trait("Requirement", "DLG-EDGE-11")]
    public async Task Attaches_a_fresh_root_store_when_the_handle_prop_changes_to_a_previously_dirtied_handle()
    {
        IRenderedComponent<DialogDirtiedHandleHarness> cut = test.Context.Render<DialogDirtiedHandleHarness>();
        DialogHandle<int?> handleB = cut.Instance.HandleB;

        await cut.InvokeAsync(() => handleB.OpenWithPayloadAsync(8));
        IElement dirty = cut.Find("[role=dialog]");
        Assert.Contains("Dirty dialog", dirty.TextContent, StringComparison.Ordinal);
        Assert.True(dirty.HasAttribute("data-open"));
        Assert.Equal("8", cut.Find("[data-testid=dirty-payload]").TextContent);

        await cut.Find("[data-testid=unmount-dirty]").ClickAsync(new MouseEventArgs());
        Assert.False(handleB.IsOpen);
        Assert.Empty(cut.FindAll("[role=dialog]"));
        Assert.Equal("No payload", cut.Find("[data-testid=payload]").TextContent);

        await cut.Find("[data-testid=switch]").ClickAsync(new MouseEventArgs());
        Assert.False(handleB.IsOpen);
        Assert.Empty(cut.FindAll("[role=dialog]"));
        Assert.Equal("No payload", cut.Find("[data-testid=payload]").TextContent);

        await test.PressAsync(cut, "trigger", open: true);
        IElement dialog = cut.Find("[role=dialog]");
        Assert.Contains("Dialog Content", dialog.TextContent, StringComparison.Ordinal);
        Assert.True(dialog.HasAttribute("data-open"));
        Assert.Equal("1", cut.Find("[data-testid=payload]").TextContent);
        Assert.Equal("true", cut.Find("#trigger").GetAttribute("aria-expanded"));
    }

    [Fact(DisplayName = "keeps a handle-backed dialog open and controllable across handle swaps on a live root")]
    [Trait("Requirement", "DLG-API-27")]
    [Trait("Requirement", "DLG-DIS-02")]
    [Trait("Requirement", "DLG-EDGE-11")]
    public async Task Keeps_a_handle_backed_dialog_open_and_controllable_across_handle_swaps_on_a_live_root()
    {
        DialogHandle<int?> handleA = new();
        DialogHandle<int?> handleB = new();
        IRenderedComponent<DialogHandleSwapHarness> cut = test.Context.Render<DialogHandleSwapHarness>(p => p
            .Add(x => x.TriggerHandle, handleA)
            .Add(x => x.RootHandle, handleA));

        await test.PressAsync(cut, "trigger", open: true);
        Assert.True(cut.Find("[role=dialog]").HasAttribute("data-open"));

        // The store is owned by the Root, so swapping the handle re-attaches it without resetting state.
        cut.Render(p => p.Add(x => x.TriggerHandle, handleB).Add(x => x.RootHandle, handleB));
        IElement dialog = cut.Find("[role=dialog]");
        Assert.True(dialog.HasAttribute("data-open"));
        Assert.True(handleB.IsOpen);
        Assert.False(handleA.IsOpen);
        Assert.Equal(dialog.Id, cut.Find("#trigger").GetAttribute("aria-controls"));

        // Swapping back to a previously-attached handle keeps working (no stale registrations).
        cut.Render(p => p.Add(x => x.TriggerHandle, handleA).Add(x => x.RootHandle, handleA));
        Assert.True(cut.Find("[role=dialog]").HasAttribute("data-open"));
        Assert.True(handleA.IsOpen);
        Assert.False(handleB.IsOpen);

        // The currently-attached handle controls the live dialog.
        await cut.InvokeAsync(() => handleA.CloseAsync());
        Assert.True(cut.Find("[role=dialog]").HasAttribute("data-closed"));
        await cut.InvokeAsync(() => cut.Instance.Root!.Unmount());
        Assert.Empty(cut.FindAll("[role=dialog]"));
    }

    [Fact(DisplayName = "attaches and detaches a handle when the prop toggles on a live root")]
    [Trait("Requirement", "DLG-API-08")]
    [Trait("Requirement", "DLG-EDGE-05")]
    [Trait("Requirement", "DLG-EDGE-11")]
    public async Task Attaches_and_detaches_a_handle_when_the_prop_toggles_on_a_live_root()
    {
        DialogHandle<int?> handle = new();
        IRenderedComponent<DialogHandleSwapHarness> cut = test.Context.Render<DialogHandleSwapHarness>(p => p
            .Add(x => x.TriggerHandle, handle)
            .Add(x => x.RootHandle, null));

        await cut.InvokeAsync(() => handle.OpenAsync("trigger"));
        Assert.False(handle.IsOpen);
        Assert.Empty(cut.FindAll("[role=dialog]"));

        cut.Render(p => p.Add(x => x.RootHandle, handle));
        await cut.InvokeAsync(() => handle.OpenAsync("trigger"));
        IElement dialog = cut.Find("[role=dialog]");
        Assert.True(dialog.HasAttribute("data-open"));
        Assert.Equal(dialog.Id, cut.Find("#trigger").GetAttribute("aria-controls"));

        await cut.InvokeAsync(() => handle.CloseAsync());
        await cut.InvokeAsync(() => cut.Instance.Root!.Unmount());
        Assert.Empty(cut.FindAll("[role=dialog]"));

        cut.Render(p => p.Add(x => x.RootHandle, null));
        await cut.InvokeAsync(() => handle.OpenAsync("trigger"));
        Assert.False(handle.IsOpen);
        Assert.Empty(cut.FindAll("[role=dialog]"));

        // Base UI warns twice. The port writes a handle's warning through the logger of the root it last attached to, so
        // the first call, made before any root attached, is dropped (PSH-EDGE-02's port note 2026-10-27).
        int detachedWarnings = numberLogger.Entries.Count(entry => entry.Message.Contains(DetachedWarning, StringComparison.Ordinal));
        Assert.Equal(1, detachedWarnings);
    }

    [Fact(DisplayName = "associates the requested trigger when opened by id in the same commit a root attaches")]
    [Trait("Requirement", "DLG-API-24")]
    public void Associates_the_requested_trigger_when_opened_by_id_in_the_same_commit_a_root_attaches()
    {
        IRenderedComponent<DialogSameCommitOpenHarness> cut = test.Context.Render<DialogSameCommitOpenHarness>();

        IElement dialog = cut.Find("[role=dialog]");
        Assert.True(dialog.HasAttribute("data-open"));

        // The requested trigger is associated (ARIA + its own payload), not the other detached trigger.
        IElement requested = cut.Find("#trigger");
        Assert.Equal("true", requested.GetAttribute("aria-expanded"));
        Assert.Equal(dialog.Id, requested.GetAttribute("aria-controls"));
        Assert.False(cut.Find("#other").HasAttribute("aria-controls"));
        Assert.Equal("5", cut.Find("[data-testid=payload]").TextContent);
    }

    // DLG-EDGE-07 restated: Base UI defers the overlap check by a 20 ms timer, ticked after both setProps; the port checks at
    // the attaching root's next render batch, so the handoff is made from the batch that mounted the incoming root
    // (DialogOverlapHarness's Then), before that check.
    [Fact(DisplayName = "port: does not warn when one root replaces another during a route transition, before the next render-batch boundary")]
    [Trait("Requirement", "DLG-EDGE-07")]
    public void Does_not_warn_when_one_root_replaces_another_during_a_route_transition()
    {
        IRenderedComponent<DialogOverlapHarness> cut = test.Context.Render<DialogOverlapHarness>();

        // The incoming root mounts while the outgoing one is still mounted (overlap), then the outgoing root unmounts,
        // completing a clean handoff, before the check runs.
        cut.Render(p => p.Add(x => x.Phase, "overlap").Add(x => x.Then, "incoming"));

        Assert.Single(cut.FindComponents<DialogRoot<NoPayload>>());
        Assert.False(cut.Instance.Handle.OverlapCheckPending);
        Assert.DoesNotContain(test.RootLogger.Entries, entry => entry.Message.Contains(OverlapWarning, StringComparison.Ordinal));
    }

    [Fact(DisplayName = "port: keeps the newer root attached after the older root unmounts before the next render-batch boundary")]
    [Trait("Requirement", "DLG-EDGE-07")]
    public async Task Keeps_the_newer_root_attached_after_the_older_root_unmounts()
    {
        IRenderedComponent<DialogOverlapHarness> cut = test.Context.Render<DialogOverlapHarness>();
        cut.Render(p => p.Add(x => x.Phase, "overlap").Add(x => x.Then, "incoming"));
        DialogHandle<NoPayload> handle = cut.Instance.Handle;

        await cut.InvokeAsync(() => handle.OpenAsync(null));

        IElement dialog = cut.Find("[role=dialog]");
        Assert.Contains("Incoming", dialog.TextContent, StringComparison.Ordinal);
        Assert.True(dialog.HasAttribute("data-open"));
        Assert.DoesNotContain("Outgoing", cut.Markup, StringComparison.Ordinal);
        Assert.True(handle.IsOpen);
    }

    [Fact(DisplayName = "port: restores control to the previous root when a newer overlapping root detaches before the next render-batch boundary")]
    [Trait("Requirement", "DLG-EDGE-07")]
    public async Task Restores_control_to_the_previous_root_when_a_newer_overlapping_root_detaches()
    {
        IRenderedComponent<DialogOverlapHarness> cut = test.Context.Render<DialogOverlapHarness>();

        // The incoming root mounts while the outgoing one is still mounted (overlap), then the transition is canceled: the
        // newer (incoming) root unmounts first while the older (outgoing) root stays mounted.
        cut.Render(p => p.Add(x => x.Phase, "overlap").Add(x => x.Then, "outgoing"));
        DialogHandle<NoPayload> handle = cut.Instance.Handle;

        // The still-mounted outgoing root regains control of the handle instead of being left inert.
        await cut.InvokeAsync(() => handle.OpenAsync(null));

        IElement dialog = cut.Find("[role=dialog]");
        Assert.Contains("Outgoing", dialog.TextContent, StringComparison.Ordinal);
        Assert.True(dialog.HasAttribute("data-open"));
        Assert.DoesNotContain("Incoming", cut.Markup, StringComparison.Ordinal);
        Assert.True(handle.IsOpen);

        // A canceled transient overlap should not warn.
        Assert.DoesNotContain(test.RootLogger.Entries, entry => entry.Message.Contains(OverlapWarning, StringComparison.Ordinal));
    }

    // DLG-EDGE-07's Strict Mode clause restated (ADR 06 Amendments 2026-10-18, rule 2): Blazor replays no effects; a root that
    // renders again runs its after-render work again, and neither re-attaches nor warns.
    [Fact(DisplayName = "port: keeps the root attached when the root renders again")]
    [Trait("Requirement", "DLG-EDGE-07")]
    public async Task Keeps_the_root_attached_when_the_root_renders_again()
    {
        IRenderedComponent<DialogOverlapHarness> cut = test.Context.Render<DialogOverlapHarness>(p => p.Add(x => x.Phase, "incoming"));
        IRenderedComponent<DialogRoot<NoPayload>> root = cut.FindComponent<DialogRoot<NoPayload>>();
        int renders = root.RenderCount;

        cut.Render();

        Assert.True(root.RenderCount > renders);
        Assert.DoesNotContain(test.RootLogger.Entries, entry => entry.Message.Contains(OverlapWarning, StringComparison.Ordinal));

        DialogHandle<NoPayload> handle = cut.Instance.Handle;
        await cut.InvokeAsync(() => handle.OpenAsync(null));

        IElement dialog = cut.Find("[role=dialog]");
        Assert.Contains("Incoming", dialog.TextContent, StringComparison.Ordinal);
        Assert.True(dialog.HasAttribute("data-open"));
        Assert.True(handle.IsOpen);
    }

    // describe "multiple triggers within Root".
    [Fact(DisplayName = "keeps the payload reactive")]
    [Trait("Requirement", "DLG-API-09")]
    [Trait("Requirement", "DLG-API-13")]
    public async Task Keeps_the_payload_reactive_within_root()
    {
        IRenderedComponent<DialogPayloadReactiveHarness> cut = test.Context.Render<DialogPayloadReactiveHarness>();

        await test.PressAsync(cut, "trigger-1", open: true);
        Assert.Equal("1", cut.Find("[data-testid=content]").TextContent);

        await cut.Find("[data-testid=update]").ClickAsync(new MouseEventArgs());
        Assert.Equal("8", cut.Find("[data-testid=content]").TextContent);
    }

    // describe "multiple detached triggers".
    [Fact(DisplayName = "keeps the payload reactive")]
    [Trait("Requirement", "DLG-API-09")]
    [Trait("Requirement", "DLG-API-13")]
    public async Task Keeps_the_payload_reactive_detached()
    {
        IRenderedComponent<DialogDetachedPayloadReactiveHarness> cut = test.Context.Render<DialogDetachedPayloadReactiveHarness>();

        await test.PressAsync(cut, "trigger-1", open: true);
        Assert.Equal("1", cut.Find("[data-testid=content]").TextContent);

        await cut.Find("[data-testid=update]").ClickAsync(new MouseEventArgs());
        Assert.Equal("8", cut.Find("[data-testid=content]").TextContent);
    }

    [Fact(DisplayName = "attaches fresh root state when the root remounts after being unmounted while open")]
    [Trait("Requirement", "DLG-EDGE-12")]
    public async Task Attaches_fresh_root_state_when_the_root_remounts_after_being_unmounted_while_open()
    {
        IRenderedComponent<DialogRemountHarness> cut = test.Context.Render<DialogRemountHarness>(p => p.Add(x => x.TriggerIds, new[] { "trigger-1", "trigger-2" }));

        await test.PressAsync(cut, "trigger-1", open: true);
        Assert.True(cut.Find("[role=dialog]").HasAttribute("data-open"));

        await cut.Find("[data-testid=unmount]").ClickAsync(new MouseEventArgs());
        Assert.Empty(cut.FindAll("[role=dialog]"));

        await cut.Find("[data-testid=remount]").ClickAsync(new MouseEventArgs());
        Assert.Empty(cut.FindAll("[role=dialog]"));
        Assert.Equal("false", cut.Find("#trigger-1").GetAttribute("aria-expanded"));

        await test.PressAsync(cut, "trigger-1", open: true);
        IElement dialog = cut.Find("[role=dialog]");
        Assert.True(dialog.HasAttribute("data-open"));
        Assert.Equal("true", cut.Find("#trigger-1").GetAttribute("aria-expanded"));
        Assert.Equal(dialog.Id, cut.Find("#trigger-1").GetAttribute("aria-controls"));
        Assert.Equal("false", cut.Find("#trigger-2").GetAttribute("aria-expanded"));
    }

    [Fact(DisplayName = "resets stale payload when a handle-backed root remounts")]
    [Trait("Requirement", "DLG-EDGE-12")]
    public async Task Resets_stale_payload_when_a_handle_backed_root_remounts()
    {
        IRenderedComponent<DialogRemountHarness> cut = test.Context.Render<DialogRemountHarness>();
        DialogHandle<int?> handle = cut.Instance.Handle;
        Assert.Equal("No payload", cut.Find("[data-testid=payload]").TextContent);

        await cut.InvokeAsync(() => handle.OpenWithPayloadAsync(8));
        Assert.True(cut.Find("[role=dialog]").HasAttribute("data-open"));
        Assert.Equal("8", cut.Find("[data-testid=payload]").TextContent);

        await cut.Find("[data-testid=unmount]").ClickAsync(new MouseEventArgs());
        Assert.Empty(cut.FindAll("[data-testid=payload]"));

        await cut.Find("[data-testid=remount]").ClickAsync(new MouseEventArgs());
        Assert.Empty(cut.FindAll("[role=dialog]"));
        Assert.Equal("No payload", cut.Find("[data-testid=payload]").TextContent);
    }

    [Fact(DisplayName = "notifies persistent detached triggers after attaching fresh root state on remount")]
    [Trait("Requirement", "DLG-ARIA-03")]
    [Trait("Requirement", "DLG-EDGE-12")]
    public async Task Notifies_persistent_detached_triggers_after_attaching_fresh_root_state_on_remount()
    {
        IRenderedComponent<DialogRemountHarness> cut = test.Context.Render<DialogRemountHarness>(p => p.Add(x => x.TriggerIds, new[] { "trigger" }));

        await test.PressAsync(cut, "trigger", open: true);
        IElement dialog = cut.Find("[role=dialog]");
        Assert.True(dialog.HasAttribute("data-open"));
        Assert.Equal("true", cut.Find("#trigger").GetAttribute("aria-expanded"));
        Assert.Equal(dialog.Id, cut.Find("#trigger").GetAttribute("aria-controls"));

        await cut.Find("[data-testid=unmount]").ClickAsync(new MouseEventArgs());
        Assert.Empty(cut.FindAll("[role=dialog]"));

        await cut.Find("[data-testid=remount]").ClickAsync(new MouseEventArgs());
        Assert.Empty(cut.FindAll("[role=dialog]"));
        Assert.Equal("false", cut.Find("#trigger").GetAttribute("aria-expanded"));
        Assert.False(cut.Find("#trigger").HasAttribute("aria-controls"));
    }

    [Fact(DisplayName = "resets stale controlled open state when a handle-backed root remounts uncontrolled")]
    [Trait("Requirement", "DLG-EDGE-12")]
    public async Task Resets_stale_controlled_open_state_when_a_handle_backed_root_remounts_uncontrolled()
    {
        IRenderedComponent<DialogControlledRemountHarness> cut = test.Context.Render<DialogControlledRemountHarness>(p => p
            .Add(x => x.First, "controlled")
            .Add(x => x.Then, "uncontrolled"));
        Assert.True(cut.Find("[role=dialog]").HasAttribute("data-open"));

        await cut.Find("[data-testid=unmount-inside]").ClickAsync(new MouseEventArgs());
        Assert.Empty(cut.FindAll("[role=dialog]"));

        await cut.Find("[data-testid=remount]").ClickAsync(new MouseEventArgs());
        Assert.Empty(cut.FindAll("[role=dialog]"));
    }

    [Fact(DisplayName = "does not warn when a handle-backed root remounts controlled after being uncontrolled")]
    [Trait("Requirement", "DLG-EDGE-12")]
    public async Task Does_not_warn_when_a_handle_backed_root_remounts_controlled_after_being_uncontrolled()
    {
        IRenderedComponent<DialogControlledRemountHarness> cut = test.Context.Render<DialogControlledRemountHarness>(p => p
            .Add(x => x.First, "uncontrolled")
            .Add(x => x.Then, "controlled"));

        await cut.Find("[data-testid=unmount]").ClickAsync(new MouseEventArgs());
        Assert.Empty(cut.FindAll("[role=dialog]"));

        await cut.Find("[data-testid=remount]").ClickAsync(new MouseEventArgs());
        Assert.True(cut.Find("[role=dialog]").HasAttribute("data-open"));

        Assert.DoesNotContain(test.RootLogger.Entries, entry =>
            entry.Message.Contains("controlled", StringComparison.Ordinal) && entry.Message.Contains("state of", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "keeps ARIA controls in sync when a detached handle is recreated while open")]
    [Trait("Requirement", "DLG-API-12")]
    [Trait("Requirement", "DLG-EDGE-11")]
    public void Keeps_ARIA_controls_in_sync_when_a_detached_handle_is_recreated_while_open()
    {
        DialogHandle<int?> first = new();
        IRenderedComponent<DialogHandleSwapHarness> cut = test.Context.Render<DialogHandleSwapHarness>(p => p
            .Add(x => x.TriggerHandle, first)
            .Add(x => x.RootHandle, first)
            .Add(x => x.ControlledOpen, true));
        Assert.Equal(cut.Find("[data-testid=popup]").Id, cut.Find("#trigger").GetAttribute("aria-controls"));

        DialogHandle<int?> second = new();
        cut.Render(p => p.Add(x => x.TriggerHandle, second).Add(x => x.RootHandle, second));

        Assert.Equal(cut.Find("[data-testid=popup]").Id, cut.Find("#trigger").GetAttribute("aria-controls"));
    }

    // describe "imperative actions on the handle".
    [Fact(
        DisplayName = "associates an imperative open-by-id with a persistent detached trigger after the root remounts",
        Skip = "Open until PR 137 lands (M4 track 6's design point 13.8, PSH-STA-28/30): PopupTriggerBehavior forgets what it forwarded once the trigger no longer owns the popup, so after the root remounts with a fresh store the trigger's unchanged payload is forwarded again. Until then the payload renders \"No payload\", expected \"7\".")]
    [Trait("Requirement", "DLG-EDGE-12")]
    public async Task Associates_an_imperative_open_by_id_with_a_persistent_detached_trigger_after_the_root_remounts()
    {
        IRenderedComponent<DialogRemountHarness> cut = test.Context.Render<DialogRemountHarness>(p => p
            .Add(x => x.TriggerIds, new[] { "trigger" })
            .Add(x => x.TriggerPayload, 7));
        DialogHandle<int?> handle = cut.Instance.Handle;

        await cut.InvokeAsync(() => handle.OpenAsync("trigger"));
        ExpectAssociated();

        await cut.Find("[data-testid=unmount]").ClickAsync(new MouseEventArgs());
        Assert.Empty(cut.FindAll("[role=dialog]"));

        await cut.Find("[data-testid=remount]").ClickAsync(new MouseEventArgs());
        Assert.Equal("false", cut.Find("#trigger").GetAttribute("aria-expanded"));

        // The trigger stayed mounted across the remount, so an imperative open-by-id must still associate it (ARIA +
        // payload), not open unassociated.
        await cut.InvokeAsync(() => handle.OpenAsync("trigger"));
        ExpectAssociated();

        void ExpectAssociated()
        {
            IElement dialog = cut.Find("[role=dialog]");
            Assert.True(dialog.HasAttribute("data-open"));
            Assert.Equal("true", cut.Find("#trigger").GetAttribute("aria-expanded"));
            Assert.Equal(dialog.Id, cut.Find("#trigger").GetAttribute("aria-controls"));
            Assert.Equal("7", cut.Find("[data-testid=payload]").TextContent);
        }
    }

    [Fact(DisplayName = "stops resolving a handle-A trigger after the root switches to handle B")]
    [Trait("Requirement", "DLG-API-24")]
    [Trait("Requirement", "DLG-EDGE-06")]
    public async Task Stops_resolving_a_handle_A_trigger_after_the_root_switches_to_handle_B()
    {
        DialogHandle<int?> handleA = new();
        DialogHandle<int?> handleB = new();
        IRenderedComponent<DialogHandleSwapHarness> cut = test.Context.Render<DialogHandleSwapHarness>(p => p
            .Add(x => x.TriggerHandle, handleA)
            .Add(x => x.RootHandle, handleA)
            .Add(x => x.TriggerId, "a")
            .Add(x => x.TriggerPayload, 1));

        // Positive control: while the root is on handle A, opening by the A-trigger's id associates it.
        await cut.InvokeAsync(() => handleA.OpenAsync("a"));
        Assert.True(cut.Find("[role=dialog]").HasAttribute("data-open"));
        Assert.Equal("true", cut.Find("#a").GetAttribute("aria-expanded"));
        await cut.InvokeAsync(() => handleA.CloseAsync());
        await cut.InvokeAsync(() => cut.Instance.Root!.Unmount());
        Assert.Empty(cut.FindAll("[role=dialog]"));

        // The A-trigger stays mounted on handle A; only the root moves to handle B.
        cut.Render(p => p.Add(x => x.RootHandle, handleB));

        // Handle B's root must no longer resolve the handle-A trigger: the trigger followed its own handle's store off the
        // root's store, so open-by-id finds nothing and warns.
        int before = numberLogger.Entries.Count;
        await cut.InvokeAsync(() => handleB.OpenAsync("a"));
        Assert.True(cut.Find("[role=dialog]").HasAttribute("data-open"));
        Assert.Equal("false", cut.Find("#a").GetAttribute("aria-expanded"));
        Assert.Contains(numberLogger.Entries.Skip(before), entry => entry.Message.Contains("No trigger found", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "does not associate the only rendered trigger when opened with a payload")]
    [Trait("Requirement", "DLG-STA-12")]
    [Trait("Requirement", "DLG-API-25")]
    [Trait("Requirement", "DLG-DOM-03")]
    public async Task Does_not_associate_the_only_rendered_trigger_when_opened_with_a_payload()
    {
        IRenderedComponent<DialogProgrammaticPayloadHarness> cut = test.Context.Render<DialogProgrammaticPayloadHarness>();
        DialogHandle<string> handle = cut.Instance.Handle;

        await cut.InvokeAsync(() => handle.OpenWithPayloadAsync("from openWithPayload"));
        Assert.Single(cut.FindAll("[role=dialog]"));
        Assert.Equal("from openWithPayload", cut.Find("[data-testid=content]").TextContent);
        IElement trigger = cut.Find("#trigger");
        Assert.Equal("false", trigger.GetAttribute("aria-expanded"));
        Assert.False(trigger.HasAttribute("aria-controls"));
        Assert.False(trigger.HasAttribute("data-popup-open"));

        await cut.InvokeAsync(() => handle.CloseAsync());
        await cut.InvokeAsync(() => cut.Instance.Root!.Unmount());
        Assert.Empty(cut.FindAll("[role=dialog]"));

        // Opening through the trigger afterwards still associates it and forwards its payload.
        await test.PressAsync(cut, "trigger", open: true);
        IElement dialog = cut.Find("[role=dialog]");
        Assert.Equal("from trigger", cut.Find("[data-testid=content]").TextContent);
        Assert.Equal("true", cut.Find("#trigger").GetAttribute("aria-expanded"));
        Assert.Equal(dialog.Id, cut.Find("#trigger").GetAttribute("aria-controls"));
    }

    [Fact(DisplayName = "does not let a trigger that mounts while open replace the programmatic payload")]
    [Trait("Requirement", "DLG-API-25")]
    [Trait("Requirement", "DLG-ARIA-11")]
    public async Task Does_not_let_a_trigger_that_mounts_while_open_replace_the_programmatic_payload()
    {
        IRenderedComponent<DialogProgrammaticPayloadHarness> cut = test.Context.Render<DialogProgrammaticPayloadHarness>(p => p.Add(x => x.ShowTrigger, false));

        await cut.InvokeAsync(() => cut.Instance.Handle.OpenWithPayloadAsync("from openWithPayload"));
        Assert.Single(cut.FindAll("[role=dialog]"));
        Assert.Equal("from openWithPayload", cut.Find("[data-testid=content]").TextContent);

        cut.Render(p => p.Add(x => x.ShowTrigger, true));

        Assert.Equal("from openWithPayload", cut.Find("[data-testid=content]").TextContent);
        IElement trigger = cut.Find("#trigger");
        Assert.Equal("false", trigger.GetAttribute("aria-expanded"));
        Assert.False(trigger.HasAttribute("aria-controls"));
    }

    [Fact(DisplayName = "keeps the programmatic payload when a controlled dialog declines a close request")]
    [Trait("Requirement", "DLG-STA-14")]
    [Trait("Requirement", "DLG-DIS-08")]
    public async Task Keeps_the_programmatic_payload_when_a_controlled_dialog_declines_a_close_request()
    {
        IRenderedComponent<DialogProgrammaticPayloadHarness> cut = test.Context.Render<DialogProgrammaticPayloadHarness>(p => p
            .Add(x => x.ShowTrigger, false)
            .Add(x => x.Controlled, true));
        DialogHandle<string> handle = cut.Instance.Handle;

        await cut.InvokeAsync(() => handle.OpenWithPayloadAsync("from openWithPayload"));
        Assert.Single(cut.FindAll("[role=dialog]"));

        // The close request is declined, so the dialog stays open without a trigger.
        await cut.InvokeAsync(() => handle.CloseAsync());
        Assert.True(cut.Find("[role=dialog]").HasAttribute("data-open"));
        Assert.True(handle.IsOpen);

        cut.Render(p => p.Add(x => x.ShowTrigger, true));

        Assert.Equal("from openWithPayload", cut.Find("[data-testid=content]").TextContent);
        IElement trigger = cut.Find("#trigger");
        Assert.Equal("false", trigger.GetAttribute("aria-expanded"));
        Assert.False(trigger.HasAttribute("aria-controls"));
    }

    private static Func<DialogTriggerState, string?> Counter(int[] renders, int index) => _ =>
    {
        renders[index]++;
        return null;
    };

    private static void ExpectOpenedBy(IRenderedComponent<DialogInactiveTriggersHarness> cut, string triggerId)
    {
        Assert.Single(cut.FindAll("[data-testid=popup]"));
        Assert.Equal("true", cut.Find($"#{triggerId}").GetAttribute("aria-expanded"));
    }

    // Base UI waits for the popup to go; here the close's exit ends with the root's Unmount().
    private static async Task ExpectClosedAsync(IRenderedComponent<DialogInactiveTriggersHarness> cut, string triggerId)
    {
        Assert.True(cut.Find("[data-testid=popup]").HasAttribute("data-closed"));
        await cut.InvokeAsync(() => cut.Instance.Root!.Unmount());
        Assert.Empty(cut.FindAll("[data-testid=popup]"));
        Assert.Equal("false", cut.Find($"#{triggerId}").GetAttribute("aria-expanded"));
    }

    // The popup's inline --nested-dialogs declaration, which C# writes in the commit path (DialogPopupTests' reading).
    private static string NestedDialogs(IElement popup)
    {
        string style = popup.GetAttribute("style") ?? string.Empty;
        const string Prefix = "--nested-dialogs: ";
        int start = style.IndexOf(Prefix, StringComparison.Ordinal);
        Assert.True(start >= 0, $"no --nested-dialogs in \"{style}\"");
        int end = style.IndexOf(';', start);
        return (end < 0 ? style[(start + Prefix.Length)..] : style[(start + Prefix.Length)..end]).Trim();
    }

    // The trigger counts are reset after the first render, as Base UI resets its bystander's.
    private IRenderedComponent<DialogInactiveTriggersHarness> RenderInactiveTriggers(int[] renders)
    {
        IRenderedComponent<DialogInactiveTriggersHarness> cut = test.Context.Render<DialogInactiveTriggersHarness>(p => p
            .Add(x => x.Trigger1ClassValue, Counter(renders, 0))
            .Add(x => x.Trigger2ClassValue, Counter(renders, 1))
            .Add(x => x.Trigger3ClassValue, Counter(renders, 2)));
        Array.Clear(renders);
        return cut;
    }

    // The interactions twin of the dialog session whose options name the popup popupId.
    private InteractionsTwin SessionTwinOf(string popupId)
    {
        IReadOnlyList<JSRuntimeInvocation> attaches = test.InteractionsModule.Invocations["attach"];
        for (int i = attaches.Count - 1; i >= 0; i--)
        {
            if (attaches[i].Arguments[1] is string options && SessionNames(options, popupId))
            {
                return ((DotNetObjectReference<InteractionsTwin>)attaches[i].Arguments[2]!).Value;
            }
        }

        throw new InvalidOperationException($"No session attached for the popup {popupId}.");
    }

    // The dismissal options the dialog session that names the popup popupId sent last (its attach, then its updates).
    private JsonElement LastDismiss(string popupId)
    {
        JsonElement? dismiss = null;
        IEnumerable<string?> sent = [.. DialogTestContext.AttachOptions(test.InteractionsModule), .. DialogTestContext.UpdateOptions(test.InteractionsHandle)];
        foreach (string? options in sent)
        {
            using JsonDocument document = JsonDocument.Parse(options!);
            if (SessionNames(document.RootElement, popupId, out JsonElement session) && session.TryGetProperty("dismiss", out JsonElement value))
            {
                dismiss = value.Clone();
            }
        }

        return dismiss ?? throw new InvalidOperationException($"No session options for the popup {popupId}.");
    }

    private static bool SessionNames(string options, string popupId)
    {
        using JsonDocument document = JsonDocument.Parse(options);
        return SessionNames(document.RootElement, popupId, out _);
    }

    private static bool SessionNames(JsonElement options, string popupId, out JsonElement session) =>
        options.TryGetProperty("session", out session)
        && session.ValueKind == JsonValueKind.Object
        && session.TryGetProperty("floatingId", out JsonElement floatingId)
        && string.Equals(floatingId.GetString(), popupId, StringComparison.Ordinal);
}

/// <summary>
/// <c>DialogRoot.detached-triggers.test.tsx</c> "does not warn for a detached payload open in production", restated over the
/// development warnings switch (Base UI's <c>NODE_ENV</c>). The switch is process-wide and on for the whole L2 run, so this
/// class turns it off only inside the non-parallel collection, where no other test runs meanwhile.
/// </summary>
[Collection(AllocationBudgetsCollection.Name)]
public sealed class DialogDetachedTriggersProductionTests : IDisposable
{
    private const string SwitchName = "Blazix.Primitives.DevelopmentWarnings";

    private readonly DialogTestContext test = new();
    private readonly CapturingLogger<DialogRoot<int?>> numberLogger = new();

    public DialogDetachedTriggersProductionTests()
    {
        test.Context.Services.AddSingleton<ILogger<DialogRoot<int?>>>(numberLogger);
    }

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "port: does not warn for a detached payload open while development warnings are off")]
    [Trait("Requirement", "DLG-EDGE-04")]
    public async Task Does_not_warn_for_a_detached_payload_open_while_development_warnings_are_off()
    {
        // A handle writes through the logger of the root it last attached to and drops a warning raised before any root
        // attached (PSH-EDGE-02's port note 2026-10-27), so a root attaches and unmounts first: the call then has a logger,
        // and only the switch keeps it silent.
        IRenderedComponent<DialogRemountHarness> cut = test.Context.Render<DialogRemountHarness>();
        DialogHandle<int?> handle = cut.Instance.Handle;
        cut.Render(p => p.Add(x => x.Mounted, false));
        int before = numberLogger.Entries.Count;

        try
        {
            AppContext.SetSwitch(SwitchName, false);
            await cut.InvokeAsync(() => handle.OpenWithPayloadAsync(8));
        }
        finally
        {
            AppContext.SetSwitch(SwitchName, true);
        }

        Assert.False(handle.IsOpen);
        Assert.Equal(before, numberLogger.Entries.Count);
    }
}
