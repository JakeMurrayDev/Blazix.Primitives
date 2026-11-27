using System.Text.Json;
using Bunit;
using Microsoft.JSInterop;
using static Iodine.Headless.Tests.Components.SliderTestContext;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Iodine.Headless.Tests.Components;

// Issue 411 (M6 track 10; the grant of orchestrator 5, 2026-11-27): the root's requests run one at a time in the module's
// order, and each sends its answering `update` without waiting for the module to apply it. On Server that answer is a round
// trip away, and the press's later reports (the gesture's end, the focus) waited behind it: the late render group of a
// track press (ADR 01's Slider row).
/// <summary>The slider root's updates to the <c>slider</c> module: sent in order, not awaited.</summary>
public sealed class SliderUpdateSendTests : IDisposable
{
    private readonly SliderTestContext test = new();

    private BunitContext Context => test.Context;

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    // A canceled change's answering update is the rollback, which the request always sends itself (no other send can have
    // carried it first), so the gesture's end queued behind it is what a round trip held on Server. bUnit fails an update
    // left unanswered after a while, which would release the queue, so the title reads the state within a short window.
    [Fact(DisplayName = "port: a request whose answering update the module has not applied yet does not hold the next report")]
    [Trait("Requirement", "SLD-STA-08")]
    [Trait("Requirement", "SLD-EVT-03")]
    public async Task Port_a_request_whose_answering_update_the_module_has_not_applied_yet_does_not_hold_the_next_report()
    {
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p
            .Add(x => x.DefaultValue, 0)
            .Add(x => x.OnValueChange, (ValueChangeEventArgs<double> e) => e.Details.Cancel())
            .AddChildContent(Anatomy()));
        await test.GestureAsync(cut, active: true, dragging: true);
        Assert.True(cut.Find("[role=group]").HasAttribute("data-dragging"));
        // From here on the module applies no update (on Server the answer is a round trip away).
        JSRuntimeInvocationHandler pending = test.SliderHandle.SetupVoid("update", _ => true);

        await test.RequestAsync(cut, "track-press", 0, [70]);
        Assert.True(JsonDocument.Parse(test.SliderUpdates[^1]).RootElement.GetProperty("rollback").GetBoolean());
        await test.GestureAsync(cut, active: false, dragging: false, commit: true);

        await WaitUntilAsync(() => !cut.Find("[role=group]").HasAttribute("data-dragging"), TimeSpan.FromMilliseconds(250));
        bool dragging = cut.Find("[role=group]").HasAttribute("data-dragging");
        await cut.InvokeAsync(pending.SetVoidResult);
        Assert.False(dragging);
    }

    // PR 398's lesson: options whose send faulted were never applied, so a later send with the same options is not skipped.
    [Fact(DisplayName = "port: a faulted update is reported, and the next send with unchanged options sends them in full")]
    [Trait("Requirement", "SLD-EVT-01")]
    public async Task Port_a_faulted_update_is_reported_and_the_next_send_with_unchanged_options_sends_them_in_full()
    {
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p
            .Add(x => x.DefaultValue, 0)
            .AddChildContent(Anatomy()));
        JSRuntimeInvocationHandler faulting = test.SliderHandle.SetupVoid("update", _ => true);
        Task<Exception> unhandled = Context.Renderer.UnhandledException;

        await test.RequestAsync(cut, "keyboard", 0, [10]);
        await cut.InvokeAsync(() => faulting.SetException(new JSException("Iodine: the update failed.")));
        Exception reported = await unhandled.WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);
        Assert.IsType<JSException>(reported);

        test.SliderHandle.SetupVoid("update", _ => true).SetVoidResult();
        int sent = test.SliderUpdates.Count;
        // Only the consumer's class changes: the root renders again with the options of the faulted send.
        RenderTolerating(() => cut.Render(p => p.Add(x => x.DefaultValue, 0).AddUnmatched("class", "consumer").AddChildContent(Anatomy())));

        await WaitUntilAsync(() => test.SliderUpdates.Count > sent, TimeSpan.FromSeconds(2));
        Assert.True(test.SliderUpdates.Count > sent, "no update after the faulted one");
        JsonElement options = JsonDocument.Parse(test.SliderUpdates[^1]).RootElement;
        Assert.Equal(10, options.GetProperty("values")[0].GetDouble());
        Assert.Equal(0, options.GetProperty("min").GetDouble());
        Assert.Equal(100, options.GetProperty("max").GetDouble());
    }

    // PR 415's verifier: a rollback whose send faulted after a later send overtook it is still owed to the module, which
    // otherwise keeps the canceled change's optimistic value (an awaited send could not be overtaken).
    [Fact(DisplayName = "port: a rollback whose send faults after a later send overtook it is sent again")]
    [Trait("Requirement", "SLD-EVT-03")]
    public async Task Port_a_rollback_whose_send_faults_after_a_later_send_overtook_it_is_sent_again()
    {
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p
            .Add(x => x.DefaultValue, 0)
            .Add(x => x.OnValueChange, (ValueChangeEventArgs<double> e) =>
            {
                if (e.Value == 70)
                {
                    e.Details.Cancel();
                }
            })
            .AddChildContent(Anatomy()));
        JSRuntimeInvocationHandler rollback = test.SliderHandle.SetupVoid("update", _ => true);
        Task<Exception> unhandled = Context.Renderer.UnhandledException;

        await test.RequestAsync(cut, "track-press", 0, [70]);
        Assert.True(JsonDocument.Parse(test.SliderUpdates[^1]).RootElement.GetProperty("rollback").GetBoolean());
        test.SliderHandle.SetupVoid("update", _ => true).SetVoidResult();
        await test.RequestAsync(cut, "keyboard", 0, [20]);
        Assert.False(JsonDocument.Parse(test.SliderUpdates[^1]).RootElement.GetProperty("rollback").GetBoolean());
        int sent = test.SliderUpdates.Count;

        await cut.InvokeAsync(() => rollback.SetException(new JSException("Iodine: the update failed.")));
        Exception reported = await unhandled.WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);
        Assert.IsType<JSException>(reported);

        await WaitUntilAsync(() => test.SliderUpdates.Count > sent, TimeSpan.FromSeconds(2));
        Assert.True(test.SliderUpdates.Count > sent, "no update after the faulted rollback");
        JsonElement options = JsonDocument.Parse(test.SliderUpdates[^1]).RootElement;
        Assert.True(options.GetProperty("rollback").GetBoolean());
        Assert.Equal(20, options.GetProperty("values")[0].GetDouble());
    }

    private static void RenderTolerating(Action render)
    {
        try
        {
            render();
        }
        catch (JSException)
        {
            // The faulted update surfaces through the renderer once.
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan window)
    {
        DateTime deadline = DateTime.UtcNow + window;
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10, Xunit.TestContext.Current.CancellationToken);
        }
    }
}
