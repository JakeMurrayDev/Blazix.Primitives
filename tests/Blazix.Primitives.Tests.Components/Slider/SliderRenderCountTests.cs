using Bunit;
using Microsoft.AspNetCore.Components;
using static Blazix.Primitives.Tests.Components.SliderTestContext;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// Design point 3 (a) of M6 track 4: a value step renders only the parts that read the value. The standing rule
// "render-count claims are measured": each title counts the renders of every part across one step, the `slider` module's
// key-press report, after two warm-up steps (the Indicator's first two steps after the attach render it while its style
// freezes, SliderIndicator's frozen selector). A part with a state function reads the value (its state's `Values`), so
// it renders on the step; a part without one renders only for what it projects from the value.
/// <summary>The renders a value step causes.</summary>
public sealed class SliderRenderCountTests : IDisposable
{
    private readonly SliderTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    // Each part's class function runs once per render of that part. The step arrives outside an event dispatch, so each
    // render is its own batch: a part renders once for its own subscription and once more for each ancestor with a state
    // function that renders, since an ancestor's render passes its child content again. Measured 2026-11-19: the Root 1,
    // the Label, the Value and the Control 2, the Track 3, the Indicator and the Thumb 4.
    [Fact(DisplayName = "port: a value step renders every part with a state function")]
    [Trait("Requirement", "SLD-STA-05")]
    [Trait("Requirement", "ADR16-7")]
    public async Task Port_a_value_step_renders_every_part_with_a_state_function()
    {
        Dictionary<string, int> renders = new()
        {
            ["root"] = 0, ["label"] = 0, ["value"] = 0, ["control"] = 0, ["track"] = 0, ["indicator"] = 0, ["thumb"] = 0,
        };
        Func<TState, string?> Count<TState>(string part) => _ =>
        {
            renders[part]++;
            return null;
        };

        IRenderedComponent<SliderRoot> cut = test.Context.Render<SliderRoot>(p => p
            .Add(x => x.DefaultValue, 50)
            .Add(x => x.ClassValue, Count<SliderRootState>("root"))
            .AddChildContent(Fragments(
                Part<SliderLabel>(l => l.Add(x => x.ClassValue, Count<SliderLabelState>("label")).AddChildContent("Volume")),
                Part<SliderValue>(v => v.Add(x => x.ClassValue, Count<SliderValueState>("value"))),
                Part<SliderControl>(c => c.Add(x => x.ClassValue, Count<SliderControlState>("control")).AddChildContent(
                    Part<SliderTrack>(t => t.Add(x => x.ClassValue, Count<SliderTrackState>("track")).AddChildContent(Fragments(
                        Part<SliderIndicator>(i => i.Add(x => x.ClassValue, Count<SliderIndicatorState>("indicator"))),
                        Thumb(parameters: t => t.Add(x => x.ClassValue, Count<SliderThumbState>("thumb")))))))))));
        await StepAsync(cut, 51);
        await StepAsync(cut, 52);
        Dictionary<string, int> before = new(renders);

        await StepAsync(cut, 53);

        Assert.Equal("53", Inputs(cut)[0].GetAttribute("aria-valuenow"));
        Assert.Equal(
            new Dictionary<string, int> { ["root"] = 1, ["label"] = 2, ["value"] = 2, ["control"] = 2, ["track"] = 3, ["indicator"] = 4, ["thumb"] = 4 },
            renders.ToDictionary(entry => entry.Key, entry => entry.Value - before[entry.Key]));
    }

    // Without state functions, the Root, the Control and the Track are counted through their child content, which each
    // passes on once per render, and the Label, the Value, the Indicator and the Thumb, which render no component, through
    // bUnit's render counts. Measured 2026-11-19: the Thumb (its input's value and `aria-valuenow`) and the Value (its text)
    // render once; the Root, the Label, the Control, the Track and the Indicator (whose style is the module's from the
    // attach on) do not.
    [Fact(DisplayName = "port: a value step renders only the thumb and the value when no part has a state function")]
    [Trait("Requirement", "SLD-STA-05")]
    [Trait("Requirement", "ADR16-7")]
    public async Task Port_a_value_step_renders_only_the_thumb_and_the_value()
    {
        int root = 0;
        int control = 0;
        int track = 0;
        static RenderFragment Counting(Action counted, RenderFragment content) => builder =>
        {
            counted();
            builder.AddContent(0, content);
        };

        IRenderedComponent<SliderRoot> cut = test.Context.Render<SliderRoot>(p => p
            .Add(x => x.DefaultValue, 50)
            .AddChildContent(Counting(() => root++, Fragments(
                Part<SliderLabel>(l => l.AddChildContent("Volume")),
                Part<SliderValue>(),
                Part<SliderControl>(c => c.AddChildContent(Counting(() => control++,
                    Part<SliderTrack>(t => t.AddChildContent(Counting(() => track++, Fragments(
                        Part<SliderIndicator>(),
                        Thumb())))))))))));
        IRenderedComponent<SliderLabel> label = cut.FindComponent<SliderLabel>();
        IRenderedComponent<SliderValue> value = cut.FindComponent<SliderValue>();
        IRenderedComponent<SliderIndicator> indicator = cut.FindComponent<SliderIndicator>();
        IRenderedComponent<SliderThumb> thumb = cut.FindComponent<SliderThumb>();
        await StepAsync(cut, 51);
        await StepAsync(cut, 52);
        int[] before = [root, label.RenderCount, value.RenderCount, control, track, indicator.RenderCount, thumb.RenderCount];

        await StepAsync(cut, 53);

        Assert.Equal("53", Inputs(cut)[0].GetAttribute("aria-valuenow"));
        Assert.Equal("53", cut.Find("output").TextContent);
        int[] after = [root, label.RenderCount, value.RenderCount, control, track, indicator.RenderCount, thumb.RenderCount];
        Assert.Equal([0, 0, 1, 0, 0, 0, 1], after.Zip(before, (a, b) => a - b).ToArray());
    }

    // The Thumb and the Value without state functions, every other part with a counting class function: the ancestors'
    // renders pass their child content again, yet the Thumb and the Value, counted through bUnit's render counts, render
    // once each, for the value they show. Measured 2026-11-19: the Thumb and the Value 1; the Root 1, the Label and the
    // Control 2, the Track 3, the Indicator 4.
    [Fact(DisplayName = "port: a value step renders the thumb and the value once when only they have no state function")]
    [Trait("Requirement", "SLD-STA-05")]
    [Trait("Requirement", "ADR16-7")]
    public async Task Port_a_value_step_renders_the_thumb_and_the_value_once_when_only_they_have_no_state_function()
    {
        Dictionary<string, int> renders = new() { ["root"] = 0, ["label"] = 0, ["control"] = 0, ["track"] = 0, ["indicator"] = 0 };
        Func<TState, string?> Count<TState>(string part) => _ =>
        {
            renders[part]++;
            return null;
        };

        IRenderedComponent<SliderRoot> cut = test.Context.Render<SliderRoot>(p => p
            .Add(x => x.DefaultValue, 50)
            .Add(x => x.ClassValue, Count<SliderRootState>("root"))
            .AddChildContent(Fragments(
                Part<SliderLabel>(l => l.Add(x => x.ClassValue, Count<SliderLabelState>("label")).AddChildContent("Volume")),
                Part<SliderValue>(),
                Part<SliderControl>(c => c.Add(x => x.ClassValue, Count<SliderControlState>("control")).AddChildContent(
                    Part<SliderTrack>(t => t.Add(x => x.ClassValue, Count<SliderTrackState>("track")).AddChildContent(Fragments(
                        Part<SliderIndicator>(i => i.Add(x => x.ClassValue, Count<SliderIndicatorState>("indicator"))),
                        Thumb()))))))));
        IRenderedComponent<SliderValue> value = cut.FindComponent<SliderValue>();
        IRenderedComponent<SliderThumb> thumb = cut.FindComponent<SliderThumb>();
        await StepAsync(cut, 51);
        await StepAsync(cut, 52);
        Dictionary<string, int> before = new(renders);
        (int Value, int Thumb) leaves = (value.RenderCount, thumb.RenderCount);

        await StepAsync(cut, 53);

        Assert.Equal("53", Inputs(cut)[0].GetAttribute("aria-valuenow"));
        Assert.Equal((1, 1), (value.RenderCount - leaves.Value, thumb.RenderCount - leaves.Thumb));
        Assert.Equal(
            new Dictionary<string, int> { ["root"] = 1, ["label"] = 2, ["control"] = 2, ["track"] = 3, ["indicator"] = 4 },
            renders.ToDictionary(entry => entry.Key, entry => entry.Value - before[entry.Key]));
    }

    // A key press, then a call queued behind the root's posted sync of its twin, so the step's work has finished.
    private async Task StepAsync(IRenderedComponent<SliderRoot> cut, double value)
    {
        await test.RequestAsync(cut, "keyboard", 0, [value]);
        await cut.InvokeAsync(() => { });
    }
}
