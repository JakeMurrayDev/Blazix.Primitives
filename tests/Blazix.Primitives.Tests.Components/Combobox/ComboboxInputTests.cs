using AngleSharp.Dom;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// CMB §3.7 at L2: the input's own handlers. The `composite` list handle declines the consumer's onkeydown and runs it from the
// part's own handler, once per event (a decision on WebAssembly runs it inside the dispatch instead).
/// <summary>Combobox's Input at L2.</summary>
public sealed class ComboboxInputTests : IDisposable
{
    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "port: the consumer's onkeydown on the input runs once per keydown")]
    [Trait("Requirement", "RND-API-04")]
    public async Task The_consumers_onkeydown_runs_once_per_keydown()
    {
        List<string> keys = [];
        void Extra(RenderTreeBuilder builder)
        {
            builder.OpenComponent<ComboboxInput>(0);
            builder.AddComponentParameter(1, "data-testid", "input");
            builder.AddComponentParameter(2, "onkeydown", EventCallback.Factory.Create<KeyboardEventArgs>(this, e => keys.Add(e.Key)));
            builder.CloseComponent();
        }

        IRenderedComponent<ComboboxHarness> cut = test.Context.Render<ComboboxHarness>(p => p
            .Add(x => x.WithInput, false)
            .Add(x => x.Extra, (RenderFragment)Extra));

        await cut.InvokeAsync(() => cut.Find("[data-testid=input]").KeyDown(new KeyboardEventArgs { Key = "a" }));

        Assert.Equal(["a"], keys);
    }

    // ADR 04 Amendments 2026-11-10 (design point 6, chosen by the user): one keydown reaches the `combobox` input handle's
    // `navigate` and the `composite` reference handle's; C# runs the consumer's onkeydown once for that event and answers
    // both, and Blazor's later dispatch of the key skips it. On Server a key the module stopped arrives as OnKeyHandled.
    [Fact(DisplayName = "port: the input's onkeydown runs once for both handles' navigate, and its PreventPartHandler() vetoes both")]
    [Trait("Requirement", "CMB-KEY-04")]
    [Trait("Requirement", "CMB-KEY-06")]
    public async Task The_inputs_onkeydown_runs_once_for_both_navigate_points()
    {
        List<string> keys = [];
        void Extra(RenderTreeBuilder builder)
        {
            builder.OpenComponent<ComboboxInput>(0);
            builder.AddComponentParameter(1, "data-testid", "input");
            builder.AddComponentParameter(2, "onkeydown", EventCallback.Factory.Create<KeyboardEventArgs>(this, e =>
            {
                keys.Add(e.Key);
                if (e.Key == "Home")
                {
                    e.PreventPartHandler();
                }
            }));
            builder.CloseComponent();
        }

        IRenderedComponent<ComboboxHarness> cut = test.Context.Render<ComboboxHarness>(p => p
            .Add(x => x.WithInput, false)
            .Add(x => x.Extra, (RenderFragment)Extra));
        Assert.Contains("\"decidesKeyDown\":true", test.LastOptions(ComboboxTwin.InputPart), StringComparison.Ordinal);
        ComboboxTwin input = test.TwinOf(ComboboxTwin.InputPart);
        JSRuntimeInvocation attach = test.Composite.Invocations["attach"].Last(invocation => invocation.Arguments[1] is string json && json.Contains("\"reference\":{}", StringComparison.Ordinal));
        CompositeTwin reference = ((DotNetObjectReference<CompositeTwin>)attach.Arguments[2]!).Value;
        string home = ComboboxTestContext.Event(ComboboxTestContext.Key("Home"), "keydown", 2);
        string end = ComboboxTestContext.Event(ComboboxTestContext.Key("End"), "keydown", 3);

        Assert.Equal("stop", await cut.InvokeAsync(() => input.Decide(ComboboxTwin.NavigatePoint, home)));
        Assert.Equal("stop", await cut.InvokeAsync(() => reference.Decide(CompositeTwin.NavigatePoint, home)));
        await cut.InvokeAsync(() => cut.Find("[data-testid=input]").KeyDown(new KeyboardEventArgs { Key = "Home", Code = "Home" }));
        Assert.Equal(["Home"], keys);

        await cut.InvokeAsync(() => input.OnKeyHandled(end));
        await cut.InvokeAsync(() => input.OnKeyHandled(end));
        await cut.InvokeAsync(() => cut.Find("[data-testid=input]").KeyDown(new KeyboardEventArgs { Key = "a" }));

        Assert.Equal(["Home", "End", "a"], keys);
    }

    [Fact(DisplayName = "port: the consumer's onclick and onmousedown on the input run once per event")]
    [Trait("Requirement", "RND-API-04")]
    public async Task The_consumers_onclick_and_onmousedown_run_once()
    {
        List<string> events = [];
        void Extra(RenderTreeBuilder builder)
        {
            builder.OpenComponent<ComboboxInput>(0);
            builder.AddComponentParameter(1, "data-testid", "input");
            builder.AddComponentParameter(2, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, _ => events.Add("click")));
            builder.AddComponentParameter(3, "onmousedown", EventCallback.Factory.Create<MouseEventArgs>(this, _ => events.Add("mousedown")));
            builder.CloseComponent();
        }

        IRenderedComponent<ComboboxHarness> cut = test.Context.Render<ComboboxHarness>(p => p
            .Add(x => x.WithInput, false)
            .Add(x => x.Extra, (RenderFragment)Extra));

        await cut.InvokeAsync(() => cut.Find("[data-testid=input]").MouseDown());
        await cut.InvokeAsync(() => cut.Find("[data-testid=input]").Click());

        Assert.Equal(["mousedown", "click"], events);
    }

    [Fact(DisplayName = "port: an input inside another family's positioner stays outside the combobox's own popup")]
    [Trait("Requirement", "CMB-STR-03")]
    public void An_input_inside_another_familys_positioner_stays_outside()
    {
        PositionerBehavior foreign = new(test.Context.JSInterop.JSRuntime, () => { }, action => { action(); return Task.CompletedTask; });
        RenderFragment harness = builder =>
        {
            builder.OpenComponent<ComboboxHarness>(0);
            builder.AddComponentParameter(1, nameof(ComboboxHarness.RootParameters), new Dictionary<string, object?> { ["Id"] = "fruit" });
            builder.CloseComponent();
        };

        IRenderedComponent<IComponent> cut = test.Context.Render(foreign.Context.Cascade(harness));
        IElement input = cut.Find("[data-testid=input]");

        Assert.Equal("combobox", input.GetAttribute("role"));
        Assert.Equal("fruit", input.Id);
        Assert.False(cut.FindComponent<ComboboxHarness>().Instance.Root!.Store.State.InputInsidePopup);
    }

    [Fact(DisplayName = "port: a combobox popup inside another family's positioner but outside its own throws the missing positioner error")]
    [Trait("Requirement", "CMB-STR-02")]
    public void A_popup_outside_its_own_positioner_throws()
    {
        PositionerBehavior foreign = new(test.Context.JSInterop.JSRuntime, () => { }, action => { action(); return Task.CompletedTask; });
        RenderFragment harness = builder =>
        {
            builder.OpenComponent<ComboboxHarness>(0);
            builder.AddComponentParameter(1, nameof(ComboboxHarness.Extra), (RenderFragment)(extra =>
            {
                extra.OpenComponent<ComboboxPopup>(0);
                extra.CloseComponent();
            }));
            builder.CloseComponent();
        };

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => test.Context.Render(foreign.Context.Cascade(harness)));

        Assert.Equal("Blazix: `<ComboboxPopup>` and `<ComboboxArrow>` must be used within the `<ComboboxPositioner>` component.", error.Message);
    }

    [Fact(DisplayName = "port: an inline input keeps its field's state")]
    [Trait("Requirement", "CMB-STA-05")]
    public void An_inline_input_keeps_its_fields_state()
    {
        RenderFragment content = builder =>
        {
            builder.OpenComponent<ComboboxHarness>(0);
            builder.AddComponentParameter(1, nameof(ComboboxHarness.RootParameters), new Dictionary<string, object?> { ["Inline"] = true });
            builder.CloseComponent();
        };

        IRenderedComponent<FieldRoot> cut = test.Context.Render<FieldRoot>(p => p.Add(x => x.Touched, true).AddChildContent(content));

        Assert.True(cut.Find("[data-testid=input]").HasAttribute("data-touched"));
    }
}
