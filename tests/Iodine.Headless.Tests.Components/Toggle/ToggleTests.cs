using AngleSharp.Dom;
using Blazix.Primitives.Rendering;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

// The namespace is the project's root: a namespace named after the family would hide the part's type.
namespace Blazix.Primitives.Tests.Components;

// Toggle.test.tsx's standalone titles verbatim, and port titles (marked as such) for the Test: none rows C# observes and
// the twin and gate wiring. A click is Blazor's dispatch of the part's own onclick (TGL-PTR-01); the browser halves (the
// default-prevented disabled click, keyboard activation, the vetoes per mode) are tests/browser/toggle.spec.ts. The
// grouped titles are ToggleGroupTests.
/// <summary>The standalone toggle: its element, its pressed request, its button twin and its composite button mode.</summary>
public sealed class ToggleTests : IDisposable
{
    private readonly ToggleTestContext test = new();

    private BunitContext Context => test.Context;

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "controlled")]
    [Trait("Requirement", "TGL-API-01")]
    [Trait("Requirement", "TGL-STA-05")]
    [Trait("Requirement", "TGL-ARIA-01")]
    public void Controlled()
    {
        IRenderedComponent<Toggle> cut = Context.Render<Toggle>(p => p.Add(x => x.Pressed, false));

        Assert.Equal("false", cut.Find("button").GetAttribute("aria-pressed"));

        cut.Render(p => p.Add(x => x.Pressed, true));
        Assert.Equal("true", cut.Find("button").GetAttribute("aria-pressed"));

        cut.Render(p => p.Add(x => x.Pressed, false));
        Assert.Equal("false", cut.Find("button").GetAttribute("aria-pressed"));
    }

    [Fact(DisplayName = "uncontrolled")]
    [Trait("Requirement", "TGL-API-02")]
    [Trait("Requirement", "TGL-STA-03")]
    [Trait("Requirement", "TGL-PTR-01")]
    [Trait("Requirement", "TGL-ARIA-01")]
    public void Uncontrolled()
    {
        IRenderedComponent<Toggle> cut = Context.Render<Toggle>(p => p.Add(x => x.DefaultPressed, false));

        Assert.Equal("false", cut.Find("button").GetAttribute("aria-pressed"));

        cut.Find("button").Click();
        Assert.Equal("true", cut.Find("button").GetAttribute("aria-pressed"));

        cut.Find("button").Click();
        Assert.Equal("false", cut.Find("button").GetAttribute("aria-pressed"));
    }

    [Fact(DisplayName = "is called when the pressed state changes")]
    [Trait("Requirement", "TGL-API-03")]
    [Trait("Requirement", "TGL-EVT-01")]
    [Trait("Requirement", "TGL-EVT-06")]
    public void Is_called_when_the_pressed_state_changes()
    {
        List<bool> calls = [];
        IRenderedComponent<Toggle> cut = Context.Render<Toggle>(p => p
            .Add(x => x.DefaultPressed, false)
            .Add(x => x.OnPressedChange, args => calls.Add(args.Value)));

        cut.Find("button").Click();

        Assert.Equal([true], calls);
    }

    [Fact(DisplayName = "does not change the pressed state when the event is canceled")]
    [Trait("Requirement", "TGL-EVT-04")]
    public void Does_not_change_the_pressed_state_when_the_event_is_canceled()
    {
        IRenderedComponent<Toggle> cut = Context.Render<Toggle>(p => p
            .Add(x => x.DefaultPressed, false)
            .Add(x => x.OnPressedChange, args => args.Details.Cancel()));

        cut.Find("button").Click();

        Assert.Equal("false", cut.Find("button").GetAttribute("aria-pressed"));
    }

    [Fact(DisplayName = "disables the component")]
    [Trait("Requirement", "TGL-API-04")]
    [Trait("Requirement", "TGL-DOM-03")]
    [Trait("Requirement", "TGL-ARIA-02")]
    [Trait("Requirement", "TGL-PTR-02")]
    [Trait("Requirement", "TGL-EVT-08")]
    public void Disables_the_component()
    {
        List<bool> calls = [];
        IRenderedComponent<Toggle> cut = Context.Render<Toggle>(p => p
            .Add(x => x.Disabled, true)
            .Add(x => x.OnPressedChange, args => calls.Add(args.Value)));

        IElement button = cut.Find("button");
        Assert.True(button.HasAttribute("disabled"));
        Assert.True(button.HasAttribute("data-disabled"));
        Assert.Equal("false", button.GetAttribute("aria-pressed"));

        // The module default-prevents the click in a browser (L4); a dispatch that still arrives is ignored.
        cut.Find("button").Click();

        Assert.Empty(calls);
        Assert.Equal("false", cut.Find("button").GetAttribute("aria-pressed"));
    }

    [Fact(DisplayName = "port: a consumer-owned toggle's click raises OnPressedChange and PressedChanged and keeps the pressed state")]
    [Trait("Requirement", "TGL-API-01")]
    [Trait("Requirement", "TGL-STA-05")]
    [Trait("Requirement", "TGL-EVT-07")]
    public void Port_a_consumer_owned_toggles_click_raises_the_callbacks_and_keeps_the_pressed_state()
    {
        List<string> order = [];
        IRenderedComponent<Toggle> cut = Context.Render<Toggle>(p => p
            .Add(x => x.Pressed, false)
            .Add(x => x.OnPressedChange, args => order.Add($"change:{args.Value}"))
            .Add(x => x.PressedChanged, value => order.Add($"bind:{value}")));

        // Neither mount nor a new Pressed raises a callback (TGL-EVT-07).
        cut.Render(p => p
            .Add(x => x.Pressed, true)
            .Add(x => x.OnPressedChange, args => order.Add($"change:{args.Value}"))
            .Add(x => x.PressedChanged, value => order.Add($"bind:{value}")));
        Assert.Empty(order);

        cut.Find("button").Click();

        Assert.Equal(["change:False", "bind:False"], order);
        Assert.Equal("true", cut.Find("button").GetAttribute("aria-pressed"));
    }

    [Fact(DisplayName = "port: ownership is fixed at the first parameter set, and a removed Pressed falls back to the initial default")]
    [Trait("Requirement", "TGL-STA-02")]
    [Trait("Requirement", "TGL-EDGE-02")]
    public void Port_ownership_is_fixed_at_the_first_parameter_set()
    {
        IRenderedComponent<Toggle> cut = Context.Render<Toggle>(p => p.Add(x => x.Pressed, false).Add(x => x.DefaultPressed, true));
        Assert.Equal("false", cut.Find("button").GetAttribute("aria-pressed"));

        cut.Render(p => p.Add(x => x.DefaultPressed, true));
        Assert.Equal("true", cut.Find("button").GetAttribute("aria-pressed"));

        // Still consumer-owned: a click does not commit.
        cut.Find("button").Click();
        Assert.Equal("true", cut.Find("button").GetAttribute("aria-pressed"));
        Assert.Contains(test.ToggleMessages, message => message.StartsWith(
            "A component is changing the controlled pressed state of Toggle to be uncontrolled.", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "port: a DefaultPressed changed after the first parameter set is logged and ignored")]
    [Trait("Requirement", "TGL-EDGE-03")]
    public void Port_a_default_changed_after_the_first_parameter_set_is_logged_and_ignored()
    {
        IRenderedComponent<Toggle> cut = Context.Render<Toggle>(p => p.Add(x => x.DefaultPressed, false));

        cut.Render(p => p.Add(x => x.DefaultPressed, true));

        Assert.Equal("false", cut.Find("button").GetAttribute("aria-pressed"));
        Assert.Contains(
            "A component is changing the default pressed state of an uncontrolled Toggle after being initialized. To suppress this warning opt to use a controlled Toggle.",
            test.ToggleMessages);
    }

    [Fact(DisplayName = "port: a standalone toggle is a tab stop; disabled, a native one is disabled and a non-native one has tabindex -1")]
    [Trait("Requirement", "TGL-FOC-01")]
    [Trait("Requirement", "TGL-DOM-05")]
    [Trait("Requirement", "TGL-DOM-04")]
    [Trait("Requirement", "TGL-ARIA-02")]
    public void Port_a_standalone_toggle_is_a_tab_stop_and_disabled_by_mode()
    {
        RenderFragment span = builder =>
        {
            builder.OpenElement(0, "span");
            builder.CloseElement();
        };

        IElement enabled = Context.Render<Toggle>().Find("button");
        IElement nativeDisabled = Context.Render<Toggle>(p => p.Add(x => x.Disabled, true)).Find("button");
        IElement nonNative = Context.Render<Toggle>(p => p.Add(x => x.NativeButton, false).Add(x => x.Render, span)).Find("span");
        IElement nonNativeDisabled = Context.Render<Toggle>(p => p.Add(x => x.NativeButton, false).Add(x => x.Disabled, true).Add(x => x.Render, span)).Find("span");

        Assert.Equal("0", enabled.GetAttribute("tabindex"));
        Assert.Equal("button", enabled.GetAttribute("type"));
        Assert.False(enabled.HasAttribute("aria-disabled"));

        Assert.True(nativeDisabled.HasAttribute("disabled"));
        Assert.False(nativeDisabled.HasAttribute("aria-disabled"));

        Assert.Equal("button", nonNative.GetAttribute("role"));
        Assert.Equal("0", nonNative.GetAttribute("tabindex"));
        Assert.False(nonNative.HasAttribute("type"));
        Assert.False(nonNative.HasAttribute("aria-disabled"));

        Assert.Equal("-1", nonNativeDisabled.GetAttribute("tabindex"));
        Assert.Equal("true", nonNativeDisabled.GetAttribute("aria-disabled"));
        Assert.False(nonNativeDisabled.HasAttribute("disabled"));
    }

    [Fact(DisplayName = "port: the details carry the reason none and the click, no trigger, and AllowPropagation changes nothing")]
    [Trait("Requirement", "TGL-EVT-02")]
    [Trait("Requirement", "TGL-EVT-03")]
    [Trait("Requirement", "TGL-EVT-09")]
    public void Port_the_details_carry_the_reason_none_and_the_click()
    {
        ChangeEventDetails? received = null;
        IRenderedComponent<Toggle> cut = Context.Render<Toggle>(p => p.Add(x => x.OnPressedChange, args =>
        {
            received = args.Details;
            args.Details.AllowPropagation();
        }));

        cut.Find("button").Click(new MouseEventArgs { Type = "click", ShiftKey = true, Detail = 1 });

        Assert.NotNull(received);
        Assert.Equal(EventReason.None, received.Reason);
        Assert.Equal(EventKind.MouseEvent, received.Event.Kind);
        Assert.Equal("click", received.Event.Type);
        Assert.True(received.Event.ShiftKey);
        Assert.Null(received.Trigger);
        Assert.Equal("true", cut.Find("button").GetAttribute("aria-pressed"));
    }

    [Fact(DisplayName = "port: a toggle renders no hidden input, drops the consumer's form and type, and never renders its Value")]
    [Trait("Requirement", "TGL-FRM-01")]
    [Trait("Requirement", "TGL-API-08")]
    [Trait("Requirement", "TGL-DOM-04")]
    [Trait("Requirement", "TGL-STR-01")]
    public void Port_a_toggle_renders_no_hidden_input_and_drops_form_and_type()
    {
        RenderFragment span = builder =>
        {
            builder.OpenElement(0, "span");
            builder.CloseElement();
        };

        IRenderedComponent<Toggle> native = Context.Render<Toggle>(p => p
            .Add(x => x.Value, "bold")
            .AddUnmatched("form", "f")
            .AddUnmatched("type", "submit")
            .AddUnmatched("name", "style"));
        IRenderedComponent<Toggle> nonNative = Context.Render<Toggle>(p => p
            .Add(x => x.NativeButton, false)
            .Add(x => x.Render, span)
            .AddUnmatched("type", "submit"));

        IElement button = Assert.Single(native.Nodes.OfType<IElement>());
        Assert.Equal("BUTTON", button.TagName);
        Assert.Equal("button", button.GetAttribute("type"));
        Assert.False(button.HasAttribute("form"));
        Assert.False(button.HasAttribute("value"));
        Assert.Equal("style", button.GetAttribute("name"));
        Assert.Empty(native.FindAll("input"));
        Assert.False(nonNative.Find("span").HasAttribute("type"));
    }

    [Fact(DisplayName = "port: a consumer attribute of the same name wins, except type")]
    [Trait("Requirement", "TGL-DOM-06")]
    [Trait("Requirement", "TGL-API-08")]
    public void Port_a_consumer_attribute_of_the_same_name_wins_except_type()
    {
        IRenderedComponent<Toggle> cut = Context.Render<Toggle>(p => p
            .Add(x => x.Disabled, true)
            .AddUnmatched("aria-pressed", "mixed")
            .AddUnmatched("tabindex", "3")
            .AddUnmatched("aria-disabled", "false")
            .AddUnmatched("data-disabled", "consumer")
            .AddUnmatched("role", "switch")
            .AddUnmatched("type", "reset"));

        IElement button = cut.Find("button");
        Assert.Equal("mixed", button.GetAttribute("aria-pressed"));
        Assert.Equal("3", button.GetAttribute("tabindex"));
        Assert.Equal("false", button.GetAttribute("aria-disabled"));
        Assert.Equal("consumer", button.GetAttribute("data-disabled"));
        Assert.Equal("switch", button.GetAttribute("role"));
        Assert.Equal("button", button.GetAttribute("type"));
    }

    [Fact(DisplayName = "port: a consumer onclick runs before the toggle's, and its PreventPartHandler() keeps the toggle unpressed")]
    [Trait("Requirement", "TGL-PTR-03")]
    [Trait("Requirement", "TGL-API-08")]
    public void Port_a_consumer_onclick_runs_first_and_can_veto_the_toggle()
    {
        List<string> order = [];
        bool veto = true;
        EventCallback<MouseEventArgs> onClick = EventCallback.Factory.Create<MouseEventArgs>(this, e =>
        {
            order.Add("consumer");
            if (veto)
            {
                e.PreventPartHandler();
            }
        });
        IRenderedComponent<Toggle> cut = Context.Render<Toggle>(p => p
            .AddUnmatched("onclick", onClick)
            .Add(x => x.OnPressedChange, args => order.Add("toggle")));

        cut.Find("button").Click();
        Assert.Equal(["consumer"], order);
        Assert.Equal("false", cut.Find("button").GetAttribute("aria-pressed"));

        veto = false;
        cut.Find("button").Click();
        Assert.Equal(["consumer", "consumer", "toggle"], order);
        Assert.Equal("true", cut.Find("button").GetAttribute("aria-pressed"));
    }

    [Fact(DisplayName = "port: a standalone toggle attaches one button twin that is not composite")]
    [Trait("Requirement", "TGL-ARIA-02")]
    [Trait("Requirement", "TGL-API-06")]
    public void Port_a_standalone_toggle_attaches_one_button_twin_that_is_not_composite()
    {
        IRenderedComponent<Toggle> cut = Context.Render<Toggle>();

        Assert.Equal(
            ["""{"disabled":false,"native":true,"composite":false,"reportInteractionType":false,"developmentChecks":true}"""],
            test.ButtonAttaches);
        Assert.Empty(test.CompositeAttaches);

        cut.Render(p => p.Add(x => x.Disabled, true));
        Assert.Equal(
            ["""{"disabled":true,"native":true,"composite":false,"reportInteractionType":false,"developmentChecks":true}"""],
            test.ButtonUpdates);
    }

    [Fact(DisplayName = "port: under a composite root without a group a toggle is in composite button mode and is not an item")]
    [Trait("Requirement", "TGL-EDGE-09")]
    [Trait("Requirement", "TGL-ARIA-02")]
    [Trait("Requirement", "TGL-STR-02")]
    [Trait("Requirement", "TGL-DOM-05")]
    public void Port_under_a_composite_root_without_a_group_a_toggle_is_in_composite_button_mode_and_is_not_an_item()
    {
        IRenderedComponent<TestCompositeRoot> cut = Context.Render<TestCompositeRoot>(p => p.AddChildContent(builder =>
        {
            builder.OpenComponent<Toggle>(0);
            builder.AddAttribute(1, "data-testid", "enabled");
            builder.CloseComponent();
            builder.OpenComponent<Toggle>(2);
            builder.AddComponentParameter(3, nameof(Toggle.Disabled), true);
            builder.AddAttribute(4, "data-testid", "disabled");
            builder.CloseComponent();
        }));

        IElement enabled = cut.Find("[data-testid=enabled]");
        IElement disabled = cut.Find("[data-testid=disabled]");

        // The composite rows of TGL-ARIA-02: no tabindex from the button writer, aria-disabled "false" when enabled.
        Assert.False(enabled.HasAttribute("tabindex"));
        Assert.Equal("false", enabled.GetAttribute("aria-disabled"));
        Assert.True(disabled.HasAttribute("disabled"));
        Assert.Equal("true", disabled.GetAttribute("aria-disabled"));
        Assert.False(enabled.HasAttribute("id"));
        Assert.All(test.ButtonAttaches, options => Assert.Contains("\"composite\":true", options, StringComparison.Ordinal));
        Assert.Equal(2, test.ButtonAttaches.Count);

        // Not an item: the root registers nothing.
        Assert.Empty(ToggleTestContext.ItemIds(test.LastCompositeOptions));
    }

    [Fact(DisplayName = "port: a declared-mode mismatch of toggles is written once per session")]
    [Trait("Requirement", "TGL-EDGE-04")]
    public void Port_a_declared_mode_mismatch_of_toggles_is_written_once_per_session()
    {
        Context.Render<Toggle>();
        Context.Render<Toggle>();

        test.ButtonTwin(0).OnNativeButtonMismatch("""{"expectedNative":true}""");
        test.ButtonTwin(1).OnNativeButtonMismatch("""{"expectedNative":true}""");

        Assert.Single(test.ToggleMessages, message => message.StartsWith(
            "Blazix: A component that acts as a button expected a native `<button>` because the `NativeButton` parameter is `true`.", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "port: the consumer's onkeydown runs once, from the activation decision or from the toggle's handler")]
    [Trait("Requirement", "TGL-API-08")]
    [Trait("Requirement", "TGL-KEY-02")]
    public async Task Port_the_consumers_onkeydown_runs_once()
    {
        int runs = 0;
        EventCallback<KeyboardEventArgs> onKeyDown = EventCallback.Factory.Create<KeyboardEventArgs>(this, e =>
        {
            runs++;
            e.PreventPartHandler();
        });
        IRenderedComponent<Toggle> cut = Context.Render<Toggle>(p => p.AddUnmatched("onkeydown", onKeyDown));

        // WebAssembly: the button module asks `activate` inside the dispatch, then Blazor dispatches the same key.
        Assert.Equal("stop", await test.DecideActivateAsync(cut, "Enter"));
        cut.Find("button").KeyDown(new KeyboardEventArgs { Key = "Enter", Code = "Enter" });
        Assert.Equal(1, runs);

        // A key no decision asked about runs from the toggle's handler.
        cut.Find("button").KeyDown(new KeyboardEventArgs { Key = "a", Code = "KeyA" });
        Assert.Equal(2, runs);
    }

    [Fact(DisplayName = "port: a render function receives the toggle's state, and the toggle draws an id to attach by")]
    [Trait("Requirement", "TGL-STA-01")]
    [Trait("Requirement", "TGL-API-07")]
    public void Port_a_render_function_receives_the_toggles_state()
    {
        List<ToggleState> states = [];
        RenderFragment<RenderProps<ToggleState>> callback = props => builder =>
        {
            states.Add(props.State);
            builder.OpenElement(0, "button");
            builder.AddMultipleAttributes(1, props.Attributes);
            builder.CloseElement();
        };
        IRenderedComponent<Toggle> cut = Context.Render<Toggle>(p => p.Add(x => x.DefaultPressed, true).Add(x => x.RenderFunction, callback));

        Assert.Equal(new ToggleState(Pressed: true, Disabled: false), states[^1]);
        Assert.StartsWith(PartIdGenerator.Prefix, cut.Find("button").GetAttribute("id"), StringComparison.Ordinal);
        Assert.Equal(cut.Find("button").GetAttribute("id"), test.ButtonModule.Invocations["attach"][0].Arguments[0]);
    }

    [Fact(DisplayName = "port: the data attribute constants are Base UI's")]
    [Trait("Requirement", "TGL-DOM-02")]
    [Trait("Requirement", "TGL-DOM-03")]
    public void Port_the_data_attribute_constants_are_base_uis()
    {
        IRenderedComponent<Toggle> cut = Context.Render<Toggle>(p => p.Add(x => x.DefaultPressed, true).Add(x => x.Disabled, true));

        Assert.Equal("data-pressed", ToggleDataAttributes.Pressed);
        Assert.Equal("data-disabled", ToggleDataAttributes.Disabled);
        Assert.Equal(string.Empty, cut.Find("button").GetAttribute(ToggleDataAttributes.Pressed));
        Assert.Equal(string.Empty, cut.Find("button").GetAttribute(ToggleDataAttributes.Disabled));
    }
}
