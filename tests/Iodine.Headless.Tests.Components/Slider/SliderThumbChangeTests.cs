using Bunit;
using Microsoft.AspNetCore.Components;
using static Iodine.Headless.Tests.Components.SliderTestContext;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Iodine.Headless.Tests.Components;

// SliderThumb.test.tsx's events › change titles and the unstable-substitute title at [L2] (docs/components/slider.md
// §13): Base UI's `fireEvent.change` is the `input-change` request the module sends for the input's new value, already
// clamped by the input (`min`/`max` attributes) and by the module (SLD-STA-16); C# applies, renders and reports it.
/// <summary>The thumb's native changes and its substitute element.</summary>
public sealed class SliderThumbChangeTests : IDisposable
{
    private readonly SliderTestContext test = new();

    private BunitContext Context => test.Context;

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    // SLD-EDGE-07. React re-creates the merged ref callback on every render; a Blazor substitute is a new fragment on every
    // render of its parent. The thumb registry settles: the thumb's renders stop growing once it has published.
    [Fact(DisplayName = "settles when the rendered component recreates its merged ref")]
    [Trait("Requirement", "SLD-EDGE-07")]
    public void Settles_when_the_rendered_component_recreates_its_merged_ref()
    {
        RenderFragment Build() => Anatomy(Thumb(parameters: t => t.Add(x => x.Render, builder =>
        {
            builder.OpenElement(0, "div");
            builder.CloseElement();
        })));

        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p.Add(x => x.DefaultValue, 50).AddChildContent(Build()));
        Assert.Single(Inputs(cut));
        IRenderedComponent<SliderThumb> thumb = cut.FindComponent<SliderThumb>();
        int settled = thumb.RenderCount;
        int attaches = test.CompositeModule.Invocations["attach"].Count;

        cut.Render(p => p.Add(x => x.DefaultValue, 50).AddChildContent(Build()));
        int afterOne = thumb.RenderCount;
        cut.Render(p => p.Add(x => x.DefaultValue, 50).AddChildContent(Build()));

        Assert.Single(Inputs(cut));
        Assert.Equal("50", Inputs(cut)[0].GetAttribute("aria-valuenow"));
        Assert.True(afterOne - settled <= 2, $"a parent render re-rendered the thumb {afterOne - settled} times");
        Assert.Equal(afterOne - settled, thumb.RenderCount - afterOne);
        Assert.Equal(attaches, test.CompositeModule.Invocations["attach"].Count);
    }

    [Fact(DisplayName = "handles change events")]
    [Trait("Requirement", "SLD-EVT-02")]
    [Trait("Requirement", "SLD-DOM-16")]
    public async Task Handles_change_events()
    {
        int changes = 0;
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p
            .Add(x => x.DefaultValue, 50)
            .Add(x => x.OnValueChange, (ValueChangeEventArgs<double> _) => changes++)
            .AddChildContent(Anatomy()));
        Assert.Equal("50", Inputs(cut)[0].GetAttribute("aria-valuenow"));

        await test.RequestAsync(cut, "input-change", 0, [51]);

        Assert.Equal(1, changes);
        Assert.Equal("51", Inputs(cut)[0].GetAttribute("aria-valuenow"));
    }

    // The input's `min`/`max` clamp 30 to 40 and 70 to 60 before the module reads the value, so the module requests the
    // clamped value each time; the second request of the same value is silent (SLD-STA-09).
    [Fact(DisplayName = "does not change the value beyond min and max")]
    [Trait("Requirement", "SLD-STA-16")]
    [Trait("Requirement", "SLD-EVT-06")]
    public async Task Does_not_change_the_value_beyond_min_and_max()
    {
        int changes = 0;
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p
            .Add(x => x.DefaultValue, 50)
            .Add(x => x.Min, 40)
            .Add(x => x.Max, 60)
            .Add(x => x.OnValueChange, (ValueChangeEventArgs<double> _) => changes++)
            .AddChildContent(Anatomy()));
        Assert.Equal("50", Inputs(cut)[0].GetAttribute("aria-valuenow"));
        Assert.Equal("40", Inputs(cut)[0].GetAttribute("min"));
        Assert.Equal("60", Inputs(cut)[0].GetAttribute("max"));

        await test.RequestAsync(cut, "input-change", 0, [40]);
        Assert.Equal("40", Inputs(cut)[0].GetAttribute("aria-valuenow"));
        Assert.Equal(1, changes);
        await test.RequestAsync(cut, "input-change", 0, [40]);
        Assert.Equal(1, changes);

        await test.RequestAsync(cut, "input-change", 0, [60]);
        Assert.Equal("60", Inputs(cut)[0].GetAttribute("aria-valuenow"));
        Assert.Equal(2, changes);
        await test.RequestAsync(cut, "input-change", 0, [60]);
        Assert.Equal(2, changes);
    }

    [Fact(DisplayName = "handles non-integer values")]
    [Trait("Requirement", "SLD-DOM-14")]
    [Trait("Requirement", "SLD-ARIA-02")]
    public async Task Handles_non_integer_values()
    {
        IRenderedComponent<SliderRoot> cut = Context.Render<SliderRoot>(p => p
            .Add(x => x.DefaultValue, 50)
            .Add(x => x.Min, -100)
            .Add(x => x.Max, 100)
            .Add(x => x.Step, 0.00000001)
            .AddChildContent(Anatomy()));
        Assert.Equal("50", Inputs(cut)[0].GetAttribute("aria-valuenow"));
        Assert.Equal("1e-8", Inputs(cut)[0].GetAttribute("step"));

        await test.RequestAsync(cut, "input-change", 0, [51.1]);
        Assert.Equal("51.1", Inputs(cut)[0].GetAttribute("aria-valuenow"));

        await test.RequestAsync(cut, "input-change", 0, [0.00000005]);
        Assert.Equal("5e-8", Inputs(cut)[0].GetAttribute("aria-valuenow"));

        await test.RequestAsync(cut, "input-change", 0, [1e-7]);
        Assert.Equal("1e-7", Inputs(cut)[0].GetAttribute("aria-valuenow"));
    }
}
