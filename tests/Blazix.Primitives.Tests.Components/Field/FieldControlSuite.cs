using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Scenario = Blazix.Primitives.Tests.Components.FieldScenarios.FieldScenario;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// <c>FieldControl.test.tsx</c>'s titles that need only Field Root, Label and Control (PR 1a), run over a control part:
/// <see cref="FieldControl"/> and <see cref="Input"/> derive the same suite, so a divergence between the two parts fails
/// (INP-STR-04, INP-STR-02). The module's reports are driven by <see cref="FieldTestContext.Module"/>.
/// </summary>
/// <typeparam name="TControl">The type of the control part.</typeparam>
public abstract class FieldControlSuite<TControl> : IDisposable
    where TControl : class, IComponent
{
    private readonly FieldTestContext host = new();

    private BunitContext Context => host.Context;

    /// <inheritdoc />
    public void Dispose()
    {
        host.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact(DisplayName = "avoids rerendering for uncontrolled input changes")]
    [Trait("Requirement", "FLD-STA-12")]
    [Trait("Requirement", "FLD-EDGE-13")]
    [Trait("Requirement", "FFI-EDGE-09")]
    public void Avoids_rerendering_for_uncontrolled_input_changes()
    {
        IRenderedComponent<FieldRoot> cut = Context.Render<FieldRoot>(p => p.AddChildContent(Control(("data-testid", "control"))));
        IRenderedComponent<TControl> control = cut.FindComponent<TControl>();
        int initial = control.RenderCount;

        host.Module.Control.Input(cut, "a");
        int afterFirstChange = control.RenderCount;
        host.Module.Control.Input(cut, "ab");
        host.Module.Control.Input(cut, "abc");

        Assert.Equal(afterFirstChange, control.RenderCount);
        Assert.True(afterFirstChange <= initial + 1, $"The first change rendered {afterFirstChange - initial} times.");
    }

    [Fact(DisplayName = "renders once per keystroke for controlled input changes")]
    [Trait("Requirement", "FLD-STA-12")]
    [Trait("Requirement", "FLD-EVT-06")]
    [Trait("Requirement", "FLD-EDGE-13")]
    public void Renders_once_per_keystroke_for_controlled_input_changes()
    {
        IRenderedComponent<FieldScenarios> cut = RenderScenario(Scenario.Controlled, p => p.Add(x => x.InitialValue, string.Empty));
        IRenderedComponent<TControl> control = cut.FindComponent<TControl>();

        host.Module.Control.Input(cut, "a");
        int settled = control.RenderCount;
        host.Module.Control.Input(cut, "ab");
        host.Module.Control.Input(cut, "abc");

        Assert.Equal(settled + 2, control.RenderCount);
    }

    [Fact(DisplayName = "validates once when changed by the user")]
    [Trait("Requirement", "FLD-EVT-02")]
    [Trait("Requirement", "FLD-EVT-07")]
    [Trait("Requirement", "FFI-EVT-02")]
    public void Validates_once_when_changed_by_the_user()
    {
        List<object?> calls = [];
        IRenderedComponent<FieldRoot> cut = Context.Render<FieldRoot>(p => p
            .Add(x => x.ValidationMode, ValidationMode.OnChange)
            .Add(x => x.Validate, args => { calls.Add(args.Value); return default; })
            .AddChildContent(Control()));

        host.Module.Control.Input(cut, "a");

        Assert.Equal(["a"], calls);
    }

    [Fact(DisplayName = "validates once when a controlled value is changed by the user")]
    [Trait("Requirement", "FLD-API-15")]
    [Trait("Requirement", "FLD-EVT-06")]
    [Trait("Requirement", "INP-API-01")]
    public void Validates_once_when_a_controlled_value_is_changed_by_the_user()
    {
        int calls = 0;
        IRenderedComponent<FieldScenarios> cut = RenderScenario(Scenario.Controlled, p => p
            .Add(x => x.InitialValue, string.Empty)
            .Add(x => x.ValidationMode, ValidationMode.OnChange)
            .Add(x => x.Validate, _ => { calls++; return default; }));

        host.Module.Control.Input(cut, "a");

        Assert.Equal(1, calls);
    }

    // FLD-EDGE-10's numeric clause has no port counterpart (Value is string?, a dated port note); the same baseline rule
    // holds for the string the consumer gives back.
    [Fact(DisplayName = "clears dirty state when a numeric controlled value returns to its initial value")]
    [Trait("Requirement", "FLD-API-15")]
    [Trait("Requirement", "FLD-STA-14")]
    [Trait("Requirement", "FLD-EDGE-10")]
    public void Clears_dirty_state_when_a_numeric_controlled_value_returns_to_its_initial_value()
    {
        IRenderedComponent<FieldScenarios> cut = RenderScenario(Scenario.Controlled, p => p.Add(x => x.InitialValue, "5"));
        Assert.False(cut.Find("[data-testid=root]").HasAttribute("data-dirty"));

        host.Module.Control.Input(cut, "56");
        Assert.Equal(string.Empty, cut.Find("[data-testid=root]").GetAttribute("data-dirty"));

        host.Module.Control.Input(cut, "5");
        Assert.False(cut.Find("[data-testid=root]").HasAttribute("data-dirty"));
    }

    [Fact(DisplayName = "syncs state and validates when the controlled value changes programmatically")]
    [Trait("Requirement", "FLD-API-15")]
    [Trait("Requirement", "FLD-EVT-03")]
    [Trait("Requirement", "FLD-EVT-06")]
    public void Syncs_state_and_validates_when_the_controlled_value_changes_programmatically()
    {
        List<object?> calls = [];
        IRenderedComponent<FieldScenarios> cut = RenderScenario(Scenario.Controlled, p => p
            .Add(x => x.InitialValue, string.Empty)
            .Add(x => x.ValidationMode, ValidationMode.OnChange)
            .Add(x => x.Validate, args => { calls.Add(args.Value); return default; }));

        cut.Find("[data-testid=set]").Click();
        host.Module.Settle(cut);

        IElement root = cut.Find("[data-testid=root]");
        Assert.Equal(string.Empty, root.GetAttribute("data-filled"));
        Assert.Equal(string.Empty, root.GetAttribute("data-dirty"));
        Assert.Equal(["external"], calls);
    }

    [Fact(DisplayName = "sets filled state on mount when the control is prefilled")]
    [Trait("Requirement", "FLD-API-16")]
    [Trait("Requirement", "FLD-STA-13")]
    public void Sets_filled_state_on_mount_when_the_control_is_prefilled()
    {
        IRenderedComponent<FieldRoot> cut = Context.Render<FieldRoot>(p => p.AddUnmatched("data-testid", "root").AddChildContent(Control(("DefaultValue", "foo"))));

        Assert.Equal(string.Empty, cut.Find("[data-testid=root]").GetAttribute("data-filled"));
    }

    [Fact(DisplayName = "does not set filled state on mount for an empty controlled value")]
    [Trait("Requirement", "FLD-STA-13")]
    public void Does_not_set_filled_state_on_mount_for_an_empty_controlled_value()
    {
        IRenderedComponent<FieldRoot> cut = Context.Render<FieldRoot>(p => p.AddUnmatched("data-testid", "root").AddChildContent(Control(("Value", string.Empty))));

        Assert.False(cut.Find("[data-testid=root]").HasAttribute("data-filled"));
    }

    [Fact(DisplayName = "clears filled state when a controlled control remounts empty")]
    [Trait("Requirement", "FLD-STA-13")]
    [Trait("Requirement", "FFI-EDGE-08")]
    public void Clears_filled_state_when_a_controlled_control_remounts_empty()
    {
        IRenderedComponent<FieldScenarios> cut = RenderScenario(Scenario.Controlled, p => p.Add(x => x.InitialValue, "value").Add(x => x.SecondKey, "false"));
        Assert.Equal(string.Empty, cut.Find("[data-testid=root]").GetAttribute("data-filled"));

        cut.Render(p => p.Add(x => x.Scenario, Scenario.Controlled).Add(x => x.ControlType, typeof(TControl)).Add(x => x.SecondKey, "true").Add(x => x.ForceValue, string.Empty));

        Assert.False(cut.Find("[data-testid=root]").HasAttribute("data-filled"));
    }

    [Fact(DisplayName = "clears filled state when an uncontrolled control remounts empty")]
    [Trait("Requirement", "FLD-STA-13")]
    [Trait("Requirement", "FFI-EDGE-08")]
    public void Clears_filled_state_when_an_uncontrolled_control_remounts_empty()
    {
        IRenderedComponent<FieldScenarios> cut = RenderScenario(Scenario.Uncontrolled, p => p.Add(x => x.DefaultValue, "value").Add(x => x.SecondKey, "false"));
        Assert.Equal(string.Empty, cut.Find("[data-testid=root]").GetAttribute("data-filled"));

        cut.Render(p => p.Add(x => x.Scenario, Scenario.Uncontrolled).Add(x => x.ControlType, typeof(TControl)).Add(x => x.SecondKey, "true").Add(x => x.DefaultValue, string.Empty));

        Assert.False(cut.Find("[data-testid=root]").HasAttribute("data-filled"));
    }

    [Fact(DisplayName = "forwards a changing defaultValue without warning")]
    [Trait("Requirement", "FLD-API-16")]
    [Trait("Requirement", "FLD-DOM-11")]
    [Trait("Requirement", "FFI-EDGE-06")]
    public void Forwards_a_changing_defaultValue_without_warning()
    {
        CapturingLogger<TControl> logger = new();
        Context.Services.AddSingleton<Microsoft.Extensions.Logging.ILogger<TControl>>(logger);
        IRenderedComponent<FieldScenarios> cut = RenderScenario(Scenario.Uncontrolled, p => p.Add(x => x.DefaultValue, string.Empty));
        Assert.False(cut.Find("[data-testid=root]").HasAttribute("data-filled"));

        cut.Render(p => p.Add(x => x.Scenario, Scenario.Uncontrolled).Add(x => x.ControlType, typeof(TControl)).Add(x => x.DefaultValue, "value"));
        host.Module.Settle(cut);

        Assert.Equal("value", host.Module.Control.DomValue);
        Assert.Equal("value", host.Module.Control.Options!.DefaultValue);
        Assert.Equal(string.Empty, cut.Find("[data-testid=root]").GetAttribute("data-filled"));
        Assert.Empty(logger.Entries);
    }

    [Fact(DisplayName = "keeps filled state from the edited value when defaultValue changes")]
    [Trait("Requirement", "FLD-API-16")]
    [Trait("Requirement", "FLD-STA-13")]
    [Trait("Requirement", "FFI-EDGE-06")]
    public void Keeps_filled_state_from_the_edited_value_when_defaultValue_changes()
    {
        IRenderedComponent<FieldScenarios> cut = RenderScenario(Scenario.Uncontrolled, p => p.Add(x => x.DefaultValue, string.Empty));
        host.Module.Control.Input(cut, "typed");
        Assert.Equal(string.Empty, cut.Find("[data-testid=root]").GetAttribute("data-filled"));

        cut.Render(p => p.Add(x => x.Scenario, Scenario.Uncontrolled).Add(x => x.ControlType, typeof(TControl)).Add(x => x.DefaultValue, "next"));
        host.Module.Settle(cut);
        cut.Render(p => p.Add(x => x.Scenario, Scenario.Uncontrolled).Add(x => x.ControlType, typeof(TControl)).Add(x => x.DefaultValue, string.Empty));
        host.Module.Settle(cut);

        Assert.Equal("typed", host.Module.Control.DomValue);
        Assert.Equal(string.Empty, cut.Find("[data-testid=root]").GetAttribute("data-filled"));
    }

    [Fact(DisplayName = "sets filled state from a controlled value on a custom element")]
    [Trait("Requirement", "FLD-API-22")]
    [Trait("Requirement", "FLD-STA-13")]
    [Trait("Requirement", "INP-API-09")]
    public void Sets_filled_state_from_a_controlled_value_on_a_custom_element()
    {
        RenderFragment div = builder =>
        {
            builder.OpenElement(0, "div");
            builder.CloseElement();
        };
        IRenderedComponent<FieldRoot> cut = Context.Render<FieldRoot>(p => p.AddUnmatched("data-testid", "root").AddChildContent(Control(("Value", "value"), ("Render", div))));

        Assert.Equal(string.Empty, cut.Find("[data-testid=root]").GetAttribute("data-filled"));
    }

    [Fact(DisplayName = "does not validate when the change is canceled")]
    [Trait("Requirement", "FLD-EVT-01")]
    [Trait("Requirement", "FLD-EVT-04")]
    [Trait("Requirement", "INP-EVT-02")]
    public void Does_not_validate_when_the_change_is_canceled()
    {
        int calls = 0;
        EventCallback<ValueChangeEventArgs<string>> cancel = EventCallback.Factory.Create<ValueChangeEventArgs<string>>(this, args =>
        {
            Assert.Equal(EventReason.None, args.Details.Reason);
            args.Details.Cancel();
        });
        IRenderedComponent<FieldRoot> cut = Context.Render<FieldRoot>(p => p
            .Add(x => x.ValidationMode, ValidationMode.OnChange)
            .Add(x => x.Validate, _ => { calls++; return default; })
            .AddChildContent(Control(("OnValueChange", cancel))));

        host.Module.Control.Input(cut, "a");

        Assert.Equal(0, calls);
    }

    [Fact(DisplayName = "writes dirty and filled for a canceled uncontrolled change")]
    [Trait("Requirement", "FLD-EVT-04")]
    [Trait("Requirement", "FLD-EVT-05")]
    public void Writes_dirty_and_filled_for_a_canceled_uncontrolled_change()
    {
        EventCallback<ValueChangeEventArgs<string>> cancel = EventCallback.Factory.Create<ValueChangeEventArgs<string>>(this, args => args.Details.Cancel());
        IRenderedComponent<FieldRoot> cut = Context.Render<FieldRoot>(p => p.AddUnmatched("data-testid", "root").AddChildContent(Control(("OnValueChange", cancel))));

        host.Module.Control.Input(cut, "a");

        Assert.True(cut.Find("[data-testid=root]").HasAttribute("data-dirty"));
        Assert.True(cut.Find("[data-testid=root]").HasAttribute("data-filled"));
    }

    [Fact(DisplayName = "validates when Enter is pressed outside a form")]
    [Trait("Requirement", "FLD-KEY-01")]
    [Trait("Requirement", "FFI-ALG-16")]
    public void Validates_when_Enter_is_pressed_outside_a_form()
    {
        int calls = 0;
        IRenderedComponent<FieldRoot> cut = Context.Render<FieldRoot>(p => p
            .Add(x => x.Validate, _ => { calls++; return default; })
            .AddChildContent(Control(("DefaultValue", "a"))));

        host.Module.Control.KeyDown(cut, "Enter");

        Assert.Equal(1, calls);
        Assert.True(cut.Find("input").HasAttribute("data-touched"));
    }

    [Fact(DisplayName = "is removed when the focused control becomes disabled")]
    [Trait("Requirement", "FLD-API-19")]
    [Trait("Requirement", "FLD-DOM-10")]
    [Trait("Requirement", "FLD-FOC-03")]
    public void Is_removed_when_the_focused_control_becomes_disabled()
    {
        IRenderedComponent<FieldScenarios> cut = RenderScenario(Scenario.TwoControls, p => p.Add(x => x.SecondMounted, false));
        cut.Find("[data-testid=first]").Focus();
        Assert.True(cut.Find("[data-testid=root]").HasAttribute("data-focused"));
        Assert.True(cut.Find("[data-testid=first]").HasAttribute("data-focused"));

        cut.Render(p => p.Add(x => x.Scenario, Scenario.TwoControls).Add(x => x.ControlType, typeof(TControl)).Add(x => x.FirstDisabled, true));

        Assert.False(cut.Find("[data-testid=root]").HasAttribute("data-focused"));
        Assert.False(cut.Find("[data-testid=first]").HasAttribute("data-focused"));
        Assert.False(cut.Find("[data-testid=label]").HasAttribute("data-focused"));
    }

    [Fact(DisplayName = "is removed when the field root becomes disabled")]
    [Trait("Requirement", "FLD-API-02")]
    [Trait("Requirement", "FLD-FOC-03")]
    public void Is_removed_when_the_field_root_becomes_disabled()
    {
        IRenderedComponent<FieldScenarios> cut = RenderScenario(Scenario.Uncontrolled);
        cut.Find("[data-testid=control]").Focus();
        Assert.True(cut.Find("[data-testid=root]").HasAttribute("data-focused"));

        cut.Render(p => p.Add(x => x.Scenario, Scenario.Uncontrolled).Add(x => x.ControlType, typeof(TControl)).Add(x => x.RootDisabled, true));

        Assert.False(cut.Find("[data-testid=root]").HasAttribute("data-focused"));
    }

    [Fact(DisplayName = "can be re-acquired after the control is re-enabled")]
    [Trait("Requirement", "FLD-FOC-04")]
    [Trait("Requirement", "FLD-FRM-11")]
    public void Can_be_re_acquired_after_the_control_is_re_enabled()
    {
        IRenderedComponent<FieldScenarios> cut = RenderScenario(Scenario.TwoControls, p => p.Add(x => x.SecondMounted, false));
        cut.Find("[data-testid=first]").Focus();
        Assert.True(cut.Find("[data-testid=root]").HasAttribute("data-focused"));

        cut.Render(p => p.Add(x => x.Scenario, Scenario.TwoControls).Add(x => x.ControlType, typeof(TControl)).Add(x => x.FirstDisabled, true));
        Assert.False(cut.Find("[data-testid=root]").HasAttribute("data-focused"));

        cut.Render(p => p.Add(x => x.Scenario, Scenario.TwoControls).Add(x => x.ControlType, typeof(TControl)).Add(x => x.FirstDisabled, false));
        Assert.False(cut.Find("[data-testid=root]").HasAttribute("data-focused"));

        cut.Find("[data-testid=first]").Focus();
        Assert.True(cut.Find("[data-testid=root]").HasAttribute("data-focused"));
    }

    [Fact(DisplayName = "is removed when the focused control unmounts")]
    [Trait("Requirement", "FLD-FOC-03")]
    [Trait("Requirement", "FLD-FRM-01")]
    public void Is_removed_when_the_focused_control_unmounts()
    {
        IRenderedComponent<FieldScenarios> cut = RenderScenario(Scenario.TwoControls, p => p.Add(x => x.SecondMounted, false));
        cut.Find("[data-testid=first]").Focus();
        Assert.True(cut.Find("[data-testid=root]").HasAttribute("data-focused"));

        cut.Render(p => p.Add(x => x.Scenario, Scenario.TwoControls).Add(x => x.ControlType, typeof(TControl)).Add(x => x.FirstMounted, false));

        Assert.False(cut.Find("[data-testid=root]").HasAttribute("data-focused"));
        Assert.False(cut.Find("[data-testid=label]").HasAttribute("data-focused"));
    }

    // The browser focuses an autofocus element before the circuit or the runtime starts; the module reports it at attach
    // (FLD-FOC-04's active check), which the stand-in drives here. The prerendered focus itself is L4 (field-autofocus).
    [Fact(DisplayName = "should sync focused state when autoFocus is used with SSR")]
    [Trait("Requirement", "FLD-API-21")]
    [Trait("Requirement", "FLD-DOM-12")]
    [Trait("Requirement", "FLD-FOC-04")]
    [Trait("Requirement", "INP-API-07")]
    public void Should_sync_focused_state_when_autoFocus_is_used_with_SSR()
    {
        IRenderedComponent<FieldRoot> cut = Context.Render<FieldRoot>(p => p
            .AddUnmatched("data-testid", "root")
            .AddChildContent<FieldLabel>(l => l.AddUnmatched("data-testid", "label").AddChildContent("Name"))
            .AddChildContent(Control(("AutoFocus", true), ("data-testid", "control"))));
        Assert.True(cut.Find("[data-testid=control]").HasAttribute("autofocus"));

        host.Module.Control.Focused(cut);

        foreach (string part in new[] { "root", "control", "label" })
        {
            Assert.Equal(string.Empty, cut.Find($"[data-testid={part}]").GetAttribute("data-focused"));
        }
    }

    [Fact(DisplayName = "updates the label association when the control is swapped")]
    [Trait("Requirement", "FLD-API-20")]
    [Trait("Requirement", "FLD-DOM-08")]
    [Trait("Requirement", "INP-API-06")]
    public void Updates_the_label_association_when_the_control_is_swapped()
    {
        IRenderedComponent<FieldScenarios> cut = RenderScenario(Scenario.Uncontrolled, p => p.Add(x => x.FirstId, "a").Add(x => x.SecondKey, "a"));
        Assert.Equal("a", cut.Find("[data-testid=control]").GetAttribute("id"));
        Assert.Equal("a", cut.Find("[data-testid=label]").GetAttribute("for"));

        cut.Render(p => p.Add(x => x.Scenario, Scenario.Uncontrolled).Add(x => x.ControlType, typeof(TControl)).Add(x => x.FirstId, "b").Add(x => x.SecondKey, "b"));

        Assert.Equal("b", cut.Find("[data-testid=control]").GetAttribute("id"));
        Assert.Equal("b", cut.Find("[data-testid=label]").GetAttribute("for"));
    }

    private IRenderedComponent<FieldScenarios> RenderScenario(Scenario scenario, Action<ComponentParameterCollectionBuilder<FieldScenarios>>? parameters = null) =>
        Context.Render<FieldScenarios>(p =>
        {
            p.Add(x => x.Scenario, scenario).Add(x => x.ControlType, typeof(TControl));
            parameters?.Invoke(p);
        });

    private static RenderFragment Control(params (string Name, object? Value)[] parameters) => builder =>
    {
        builder.OpenComponent<TControl>(0);
        int sequence = 1;
        foreach ((string name, object? value) in parameters)
        {
            builder.AddComponentParameter(sequence++, name, value);
        }

        builder.CloseComponent();
    };
}

/// <summary>The field-control suite over <see cref="FieldControl"/>.</summary>
public sealed class FieldControlTests : FieldControlSuite<FieldControl>;

/// <summary>The field-control suite over <see cref="Input"/>: Input satisfies Field.Control's requirements (INP-STR-04).</summary>
public sealed class InputFieldControlTests : FieldControlSuite<Input>;
