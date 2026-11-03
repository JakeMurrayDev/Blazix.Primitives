using System.Text.Json;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The open lifecycle on the standalone test host at L2 (ADR 05 Amendments 2026-11-03; FLT-STR-11, the port's standalone
/// root store; FLT-EVT-02's port order; TRN-ANI-22's mechanism): a root with no trigger registry or payload, an
/// <c>interactions</c> session on its token and a transitions-watched popup. Both modules are mocked: the session's
/// attach and updates are recorded, and each test reports what the <c>transitions</c> module reports through its twin.
/// </summary>
public sealed class PopupLifecycleHostTests : IDisposable
{
    private readonly LifecycleTestContext test = new();
    private TestLifecycleRoot? root;

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "port: the standalone root starts closed, renders no popup and attaches its session with the floating id")]
    [Trait("Requirement", "FLT-STR-11")]
    public void The_standalone_root_starts_closed_renders_no_popup_and_attaches_its_session_with_the_floating_id()
    {
        IRenderedComponent<TestLifecycleRoot> cut = RenderRoot();

        Assert.Empty(cut.FindAll("[role=\"dialog\"]"));
        Assert.False(root!.Store.IsOpen);
        JsonElement session = test.SessionAttach.GetProperty("session");
        Assert.False(session.GetProperty("open").GetBoolean());
        Assert.Equal(root.Store.Lifecycle.FloatingId, session.GetProperty("floatingId").GetString());
        Assert.False(test.SessionAttach.TryGetProperty("relay", out _));
    }

    [Fact(DisplayName = "port: opening without a trigger mounts the popup with the floating id and syncs the open state to the session")]
    [Trait("Requirement", "FLT-STR-11")]
    public async Task Opening_without_a_trigger_mounts_the_popup_with_the_floating_id_and_syncs_the_open_state_to_the_session()
    {
        IRenderedComponent<TestLifecycleRoot> cut = RenderRoot();

        Assert.True(await cut.InvokeAsync(() => root!.RequestOpenAsync(true, new OpenChangeEventDetails(EventReason.TriggerPress))));

        cut.WaitForAssertion(() => Assert.Equal(root!.Store.Lifecycle.FloatingId, cut.Find("[role=\"dialog\"]").Id));
        Assert.True(cut.Find("[role=\"dialog\"]").HasAttribute("data-open"));
        cut.WaitForAssertion(() => Assert.True(test.LastSessionUpdate.GetProperty("session").GetProperty("open").GetBoolean()));
    }

    // FLT-STR-11's `setOpen` dispatches `openchange` (FLT-STA-01): the port relays each committed change to the session in
    // its update. FLT-EVT-02's port order (ADR 05 Amendments 2026-11-03): the relay follows the commit, so a canceled change
    // moves no revision and sends nothing.
    [Fact(DisplayName = "port: each committed change is relayed to the session with the next revision, and a canceled one is not")]
    [Trait("Requirement", "FLT-STR-11")]
    [Trait("Requirement", "FLT-EVT-02")]
    public async Task Each_committed_change_is_relayed_to_the_session_with_the_next_revision_and_a_canceled_one_is_not()
    {
        bool cancel = false;
        IRenderedComponent<TestLifecycleRoot> cut = RenderRoot(p => p.Add(x => x.OnOpenChange, (ValueChangeEventArgs<bool> args) =>
        {
            if (cancel)
            {
                args.Details.Cancel();
            }
        }));

        await cut.InvokeAsync(() => root!.RequestOpenAsync(true, new OpenChangeEventDetails(EventReason.TriggerPress)));
        cut.WaitForAssertion(() => Assert.Equal([1], test.RelayRevisions));

        cancel = true;
        Assert.False(await cut.InvokeAsync(() => root!.RequestOpenAsync(false, new OpenChangeEventDetails(EventReason.EscapeKey))));
        cancel = false;
        await cut.InvokeAsync(() => root!.RequestOpenAsync(false, new OpenChangeEventDetails(EventReason.OutsidePress)));

        cut.WaitForAssertion(() => Assert.Equal([1, 2], test.RelayRevisions));
        Assert.Equal("outside-press", test.LastSessionUpdate.GetProperty("relay").GetProperty("reason").GetString());
    }

    // NavigationMenu's constraint (ADR 05 Amendments 2026-11-03; ADR 08 Amendments 2026-11-03 (2)): an open derived from a
    // family-owned value runs the family's own step, with cancel, and the lifecycle commits after it.
    [Fact(DisplayName = "port: a derived open state opens through the family's own step and stays open when that step cancels the close")]
    [Trait("Requirement", "FLT-STR-11")]
    public async Task A_derived_open_state_opens_through_the_familys_own_step_and_stays_open_when_that_step_cancels_the_close()
    {
        List<bool> steps = [];
        bool allow = true;
        IRenderedComponent<TestLifecycleRoot> cut = RenderRoot(p => p.Add(x => x.ValueStep, (open, _) =>
        {
            steps.Add(open);
            return allow;
        }));

        Assert.True(await cut.InvokeAsync(() => root!.RequestOpenAsync(true, new OpenChangeEventDetails(EventReason.TriggerHover))));
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[role=\"dialog\"]")));

        allow = false;
        Assert.False(await cut.InvokeAsync(() => root!.RequestOpenAsync(false, new OpenChangeEventDetails(EventReason.OutsidePress))));

        Assert.Equal([true, false], steps);
        Assert.True(root!.Store.IsOpen);
        Assert.False(root.Store.OpenState.IsControlled);
        Assert.Equal([1], test.RelayRevisions);
    }

    // TRN-ANI-22's mechanism (useUnmountAfterClose.ts:102-116, useOpenChangeComplete.tsx:24-26): the unmount action while
    // the popup is closed and still animating out unmounts it in the same commit; the exit's later completion fires no
    // second callback.
    [Fact(DisplayName = "port: an unmount while closing unmounts the popup in the same commit and its later exit completion fires no second callback")]
    [Trait("Requirement", "TRN-ANI-22")]
    [Trait("Requirement", "TRN-EVT-08")]
    public async Task An_unmount_while_closing_unmounts_the_popup_in_the_same_commit_and_its_later_exit_completion_fires_no_second_callback()
    {
        List<bool> completions = [];
        IRenderedComponent<TestLifecycleRoot> cut = RenderRoot(p => p
            .Add(x => x.DefaultOpen, true)
            .Add(x => x.OnOpenChangeComplete, (bool opened) => completions.Add(opened)));
        cut.WaitForAssertion(() => Assert.NotNull(Popup(cut).Transitions));
        TransitionsTwin transitions = Popup(cut).Transitions!;

        await cut.InvokeAsync(() => root!.RequestOpenAsync(false, new OpenChangeEventDetails(EventReason.EscapeKey)));
        await cut.InvokeAsync(() => transitions.OnStatusChange("""{"status":"ending","mounted":true}"""));
        Assert.Single(cut.FindAll("[role=\"dialog\"]"));

        await cut.InvokeAsync(root!.UnmountAsync);

        Assert.Empty(cut.FindAll("[role=\"dialog\"]"));
        Assert.Equal([false], completions);
        Assert.Equal(1, root.Store.Lifecycle.UnmountRequests);

        await cut.InvokeAsync(() => transitions.OnTransitionComplete("""{"open":false}"""));
        Assert.Equal([false], completions);
    }

    private static TestLifecyclePopup Popup(IRenderedComponent<TestLifecycleRoot> cut) => cut.FindComponent<TestLifecyclePopup>().Instance;

    private IRenderedComponent<TestLifecycleRoot> RenderRoot(Action<ComponentParameterCollectionBuilder<TestLifecycleRoot>>? parameters = null)
    {
        IRenderedComponent<TestLifecycleRoot> cut = test.Context.Render<TestLifecycleRoot>(p =>
        {
            parameters?.Invoke(p);
            p.AddChildContent<TestLifecyclePopup>();
        });
        root = cut.Instance;
        return cut;
    }
}

/// <summary>A bUnit context with the <c>interactions</c> and <c>transitions</c> modules mocked, recording the session's attach and updates.</summary>
internal sealed class LifecycleTestContext : IDisposable
{
    public LifecycleTestContext()
    {
        Context.Services.AddBlazixPrimitives();
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        InteractionsModule = Context.JSInterop.SetupModule(InteractionsTestContext.ModulePath);
        InteractionsHandle = InteractionsModule.SetupModule("attach", _ => true);
        InteractionsHandle.SetupVoid("update", _ => true).SetVoidResult();
        InteractionsHandle.SetupVoid("dispose", _ => true).SetVoidResult();
        BunitJSModuleInterop transitions = Context.JSInterop.SetupModule("./_content/Blazix.Primitives/js/transitions.js");
        BunitJSModuleInterop transitionsHandle = transitions.SetupModule("attach", _ => true);
        transitionsHandle.SetupVoid("update", _ => true).SetVoidResult();
        transitionsHandle.SetupVoid("dispose", _ => true).SetVoidResult();
    }

    public BunitContext Context { get; } = new();

    public BunitJSModuleInterop InteractionsModule { get; }

    public BunitJSModuleInterop InteractionsHandle { get; }

    public JsonElement SessionAttach => JsonDocument.Parse((string)InteractionsModule.Invocations["attach"].Single(attach => attach.Arguments[0] is null).Arguments[1]!).RootElement;

    public IReadOnlyList<JsonElement> SessionUpdates => [.. InteractionsHandle.Invocations["update"].Select(invocation => JsonDocument.Parse((string)invocation.Arguments[0]!).RootElement)];

    public JsonElement LastSessionUpdate => SessionUpdates[^1];

    /// <summary>Gets the distinct relay revisions the session was sent, in order, its attach's included.</summary>
    public IReadOnlyList<int> RelayRevisions =>
    [
        .. new[] { SessionAttach }.Concat(SessionUpdates)
            .Where(options => options.TryGetProperty("relay", out _))
            .Select(options => options.GetProperty("relay").GetProperty("revision").GetInt32())
            .Distinct(),
    ];

    public void Dispose() => Context.Dispose();
}
