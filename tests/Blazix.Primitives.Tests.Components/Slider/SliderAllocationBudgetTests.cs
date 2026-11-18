using System.Globalization;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using static Blazix.Primitives.Tests.Components.SliderTestContext;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// ADR 16 rule 7 for Slider (decision 26 of the M6 plan): a value step on an uncontrolled root without consumer callbacks,
/// a re-render of the root with unchanged parameters, and a value step whose request passes a consumer's
/// <c>OnValueChange</c> and <c>OnValueCommitted</c>. Each step is the <c>slider</c> module's <c>OnValueRequest</c> report of
/// a key press, as the module sends it after its optimistic move, measured inside the renderer's dispatcher call that
/// delivers it; it includes the report's deserialization, the request and the renders it causes. The anatomy is Base UI's
/// TestSlider: a Value, then a Control holding a Track with an Indicator and one Thumb. The budgets were taken from the
/// first measurement and only tighten afterwards.
/// </summary>
[Collection(AllocationBudgetsCollection.Name)]
public sealed class SliderAllocationBudgetTests : IDisposable
{
    // First measurement 2026-11-19 (macOS arm64, .NET 11, Release, dynamic PGO off, the class alone in its process; the
    // least of five in each of five processes, all equal). 50,864 bytes for a step without consumer callbacks: the report's
    // deserialization, the request, the inline `update` and the renders of the Thumb and the Value, the only parts that
    // render on a step once the Indicator's style is frozen (SliderRenderCountTests). A report arrives outside an event
    // dispatch, so each part's render is its own batch, and bUnit rebuilds the root's markup after each one; as in the
    // Tabs activation, that markup is most of it.
    private const long StepBudgetBytes = 50864;
    // 4,200 bytes for the re-render (2026-11-19, macOS arm64, Release, least of five, the same in five processes; 4,192 before
    // main's renders-only-with-content gate of Toast 9a, and 5,168 before SliderOptions compared its lists by element, when
    // every flush sent an update): the root and its cascade render again, and with them the parts whose child content is a
    // fragment; no update is sent. ADR 16 Amendments 2026-11-19 (the re-pin after merging main).
    private const long RerenderBudgetBytes = 4200;
    // 50,864 bytes for a step through a consumer's OnValueChange and OnValueCommitted, the same as without them: the change
    // and commit arguments are made either way, and an EventCallback over a synchronous lambda completes inline.
    private const long CallbackStepBudgetBytes = 50864;

    private readonly SliderTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "a value step on an uncontrolled slider without consumer callbacks stays within its budget")]
    [Trait("Requirement", "ADR16-7")]
    public async Task A_value_step_without_consumer_callbacks_stays_within_its_budget()
    {
        IRenderedComponent<SliderRoot> cut = test.Context.Render<SliderRoot>(p => p
            .Add(x => x.DefaultValue, 50)
            .AddChildContent(Anatomy()));

        long allocated = await MeasureStepsAsync(cut);

        Assert.Equal("51", Inputs(cut)[0].GetAttribute("aria-valuenow"));
        Assert.True(allocated <= StepBudgetBytes, $"a value step allocated {allocated} bytes; the budget is {StepBudgetBytes}");
    }

    [Fact(DisplayName = "a re-render of a slider with unchanged parameters stays within its budget and sends no update")]
    [Trait("Requirement", "ADR16-7")]
    public void A_rerender_with_unchanged_parameters_stays_within_its_budget()
    {
        RenderFragment content = Anatomy();
        IRenderedComponent<SliderRoot> cut = test.Context.Render<SliderRoot>(p => p
            .Add(x => x.DefaultValue, 50)
            .AddUnmatched("class", "consumer")
            .AddChildContent(content));

        for (int i = 0; i < 20; i++)
        {
            cut.Render(p => p.Add(x => x.DefaultValue, 50).AddUnmatched("class", "consumer").AddChildContent(content));
        }

        int updates = test.SliderUpdates.Count;
        long allocated = AllocationSampler.LeastOfFive(() =>
            cut.Render(p => p.Add(x => x.DefaultValue, 50).AddUnmatched("class", "consumer").AddChildContent(content)));

        Assert.True(allocated <= RerenderBudgetBytes, $"a re-render allocated {allocated} bytes; the budget is {RerenderBudgetBytes}");
        Assert.Equal(updates, test.SliderUpdates.Count);
    }

    [Fact(DisplayName = "a value step through a consumer's OnValueChange to its commit stays within its budget")]
    [Trait("Requirement", "ADR16-7")]
    public async Task A_value_step_through_a_consumer_callback_stays_within_its_budget()
    {
        int changes = 0;
        int commits = 0;
        IRenderedComponent<SliderRoot> cut = test.Context.Render<SliderRoot>(p => p
            .Add(x => x.DefaultValue, 50)
            .Add(x => x.OnValueChange, (ValueChangeEventArgs<double> _) => changes++)
            .Add(x => x.OnValueCommitted, (ValueCommittedEventArgs<double> _) => commits++)
            .AddChildContent(Anatomy()));

        long allocated = await MeasureStepsAsync(cut);

        Assert.Equal("51", Inputs(cut)[0].GetAttribute("aria-valuenow"));
        Assert.Equal(25, changes);
        Assert.Equal(25, commits);
        Assert.True(allocated <= CallbackStepBudgetBytes, $"a value step allocated {allocated} bytes; the budget is {CallbackStepBudgetBytes}");
    }

    // Twenty key presses alternate the value between 51 and 50, then five more are measured, each a change; the last
    // leaves 51. The reports are built before the window. A step posts the root's sync of its twin behind a Task.Yield, so
    // the next call may be queued behind that work on the renderer's synchronization context and run on another thread:
    // each run is measured inside its dispatcher call, on the thread that runs it, from the report's arrival to the end of
    // its synchronous work (the request, the renders and the inline update). The least of five absorbs a growth of bUnit's
    // invocation lists.
    private async Task<long> MeasureStepsAsync(IRenderedComponent<SliderRoot> cut)
    {
        SliderTwin twin = test.SliderTwin();
        string[] reports = [.. Enumerable.Range(1, 25).Select(i => Report(i % 2 == 1 ? 51 : 50, i))];
        for (int i = 0; i < 20; i++)
        {
            string json = reports[i];
            await cut.InvokeAsync(() => twin.OnValueRequest(json));
        }

        long allocated = long.MaxValue;
        for (int run = 0; run < AllocationSampler.Runs; run++)
        {
            string json = reports[20 + run];
            long sample = 0;
            await cut.InvokeAsync(() =>
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                twin.OnValueRequest(json);
                sample = GC.GetAllocatedBytesForCurrentThread() - before;
            });
            allocated = Math.Min(allocated, sample);
        }

        return allocated;
    }

    private static string Report(double value, int revision) =>
        $$"""{"reason":"keyboard","activeThumbIndex":0,"values":[{{value.ToString(CultureInfo.InvariantCulture)}}],"event":{{KeyJson()}},"revision":{{revision}}}""";
}
