using System.Numerics;
using Blazix.Primitives.Tests.TestSupport;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Tests.Events;

/// <summary>
/// <c>packages/utils/src/useControlled.test.tsx</c> (UTL-STA-01..06, EVD-STA-01..05, EVD-ALG-08, EVD-EDGE-01..03) and the
/// request sequence of ADR 05 §2 (EVD-ALG-03, EVD-STA-04/08, EVD-EVT-04..09, ADR 05 Amendments 2026-10-18 item 2).
/// <c>TestComponent</c> is a fake part that hands its <see cref="ParameterView"/> to <see cref="Controlled{T}"/> as its
/// <c>SetParametersAsync</c> would; <c>setProps</c> is a second parameter set. Base UI prefixes its messages with
/// <c>Base UI: </c>; the port logs the text alone through the part's logger.
/// </summary>
public sealed class ControlledTests
{
    private const string UncontrolledToControlled =
        "A component is changing the uncontrolled value state of TestComponent to be controlled.\n"
        + "Elements should not switch from uncontrolled to controlled (or vice versa).\n"
        + "Decide between using a controlled or uncontrolled TestComponent element for the lifetime of the component.\n"
        + "The nature of the state is determined during the first render. It's considered controlled if the value is not `undefined`.\n"
        + "More info: https://fb.me/react-controlled-components";

    private const string DefaultChanged =
        "A component is changing the default value state of an uncontrolled TestComponent after being initialized. To suppress this warning opt to use a controlled TestComponent.";

    [Fact(DisplayName = "works correctly when is not controlled")]
    [Trait("Requirement", "UTL-STA-01")]
    [Trait("Requirement", "UTL-STA-04")]
    [Trait("Requirement", "EVD-STA-01")]
    public void Works_correctly_when_is_not_controlled()
    {
        TestComponent<object?> component = new();
        component.SetProps(("DefaultValue", 1));
        Assert.Equal(1, component.Value);
        Assert.False(component.State.IsControlled);

        component.State.SetValueIfUncontrolled(2);

        Assert.Equal(2, component.Value);
    }

    [Fact(DisplayName = "works correctly when is controlled")]
    [Trait("Requirement", "UTL-STA-01")]
    [Trait("Requirement", "EVD-STA-01")]
    public void Works_correctly_when_is_controlled()
    {
        TestComponent<object?> component = new();
        component.SetProps(("Value", 1));

        Assert.Equal(1, component.Value);
        Assert.True(component.State.IsControlled);
    }

    [Fact(DisplayName = "warns when switching from uncontrolled to controlled")]
    [Trait("Requirement", "UTL-STA-01")]
    [Trait("Requirement", "EVD-EDGE-01")]
    public void Warns_when_switching_from_uncontrolled_to_controlled()
    {
        TestComponent<object?> component = new();
        component.SetProps();
        Assert.Empty(component.Logger.Messages);

        component.SetProps(("Value", "foobar"));

        Assert.Equal([UncontrolledToControlled], component.Logger.Messages);
        Assert.False(component.State.IsControlled);
        Assert.Null(component.Value);
    }

    [Fact(DisplayName = "warns and falls back to the default when switching from controlled to uncontrolled")]
    [Trait("Requirement", "UTL-STA-03")]
    [Trait("Requirement", "UTL-STA-04")]
    [Trait("Requirement", "EVD-STA-05")]
    [Trait("Requirement", "EVD-EDGE-01")]
    public void Warns_and_falls_back_to_the_default_when_switching_from_controlled_to_uncontrolled()
    {
        TestComponent<string?> component = new("TestHook");
        component.SetProps(("Value", "foobar"), ("DefaultValue", "default"));
        Assert.Empty(component.Logger.Messages);
        Assert.Equal("foobar", component.Value);

        component.SetProps(("DefaultValue", "default"));

        Assert.Single(component.Logger.Messages);
        Assert.StartsWith("A component is changing the controlled value state of TestHook to be uncontrolled.", component.Logger.Messages[0]);
        Assert.Equal("default", component.Value);

        component.State.SetValueIfUncontrolled("next");

        Assert.Equal("default", component.Value);
    }

    [Fact(DisplayName = "stores a function default value instead of calling it")]
    [Trait("Requirement", "UTL-STA-02")]
    [Trait("Requirement", "EVD-STA-03")]
    public void Stores_a_function_default_value_instead_of_calling_it()
    {
        int calls = 0;
        Func<string> defaultValue = () =>
        {
            calls++;
            return "called";
        };
        TestComponent<Func<string>?> component = new();

        component.SetProps(("DefaultValue", defaultValue));

        Assert.Same(defaultValue, component.Value);
        Assert.Equal(0, calls);
    }

    [Fact(DisplayName = "warns when changed after initial rendering")]
    [Trait("Requirement", "EVD-STA-02")]
    [Trait("Requirement", "EVD-EDGE-02")]
    public void Warns_when_changed_after_initial_rendering()
    {
        TestComponent<object?> component = new();
        component.SetProps();
        Assert.Empty(component.Logger.Messages);

        component.SetProps(("DefaultValue", 1));

        Assert.Equal([DefaultChanged], component.Logger.Messages);
        Assert.Null(component.Value);
    }

    [Fact(DisplayName = "does not warn when controlled")]
    [Trait("Requirement", "EVD-EDGE-02")]
    public void Does_not_warn_when_controlled()
    {
        TestComponent<object?> component = new();
        component.SetProps(("Value", 1), ("DefaultValue", 0));
        Assert.Empty(component.Logger.Messages);

        component.SetProps(("Value", 1), ("DefaultValue", 1));

        Assert.Empty(component.Logger.Messages);
    }

    [Fact(DisplayName = "does not warn when NaN")]
    [Trait("Requirement", "EVD-EDGE-03")]
    public void Does_not_warn_when_NaN()
    {
        TestComponent<double> component = new();

        component.SetProps(("DefaultValue", double.NaN));
        component.SetProps(("DefaultValue", double.NaN));

        Assert.Empty(component.Logger.Messages);
    }

    [Fact(DisplayName = "does not warn when an array")]
    [Trait("Requirement", "EVD-EDGE-03")]
    public void Does_not_warn_when_an_array()
    {
        TestComponent<int[]?> component = new();

        // A consumer that writes the array inline passes a new, equal instance on every parameter set.
        component.SetProps(("DefaultValue", Array.Empty<int>()));
        component.SetProps(("DefaultValue", new int[0]));

        Assert.Empty(component.Logger.Messages);
    }

    [Fact(DisplayName = "does not throw when defaultValue has React elements")]
    [Trait("Requirement", "EVD-EDGE-03")]
    public void Does_not_throw_when_defaultValue_has_React_elements()
    {
        TestComponent<Holder?> component = new();
        RenderFragment span = builder => builder.AddMarkupContent(0, "<span></span>");

        component.SetProps(("DefaultValue", new Holder(span)));
        component.SetProps(("DefaultValue", new Holder(span)));

        Assert.Empty(component.Logger.Messages);
    }

    [Fact(DisplayName = "does not throw when defaultValue has function")]
    [Trait("Requirement", "EVD-EDGE-03")]
    public void Does_not_throw_when_defaultValue_has_function()
    {
        TestComponent<Holder?> component = new();
        Func<int> fn = () => 100;

        component.SetProps(("DefaultValue", new Holder(fn)));
        component.SetProps(("DefaultValue", new Holder(fn)));

        Assert.Empty(component.Logger.Messages);
    }

    [Fact(DisplayName = "does not throw when defaultValue has bigint")]
    [Trait("Requirement", "EVD-EDGE-03")]
    public void Does_not_throw_when_defaultValue_has_bigint()
    {
        TestComponent<BigInteger> component = new();

        component.SetProps(("DefaultValue", new BigInteger(1)));
        component.SetProps(("DefaultValue", new BigInteger(1)));

        Assert.Empty(component.Logger.Messages);
    }

    [Fact(DisplayName = "should warn only when defaultValue changes")]
    [Trait("Requirement", "EVD-STA-02")]
    [Trait("Requirement", "EVD-EDGE-02")]
    public void Should_warn_only_when_defaultValue_changes()
    {
        TestComponent<object?> component = new();
        component.SetProps(("DefaultValue", 0));
        Assert.Empty(component.Logger.Messages);

        component.SetProps(("DefaultValue", 1));
        Assert.Equal([DefaultChanged], component.Logger.Messages);

        component.SetProps(("DefaultValue", 2));
        Assert.Single(component.Logger.Messages);

        component.SetProps(("DefaultValue", 0));
        Assert.Single(component.Logger.Messages);
    }

    [Fact(DisplayName = "should warn only when defaultValue has functions/components and changes")]
    [Trait("Requirement", "EVD-EDGE-02")]
    public void Should_warn_only_when_defaultValue_has_functions_components_and_changes()
    {
        RenderFragment span = builder => builder.AddMarkupContent(0, "<span></span>");
        Func<int> fn = () => 100;
        RenderFragment div = builder => builder.AddMarkupContent(0, "<div></div>");
        Holder[] items = [new(span), new(fn), new(div)];
        TestComponent<Holder?> component = new();

        component.SetProps(("DefaultValue", items[0]));
        Assert.Empty(component.Logger.Messages);

        component.SetProps(("DefaultValue", items[1]));
        Assert.Equal([DefaultChanged], component.Logger.Messages);

        component.SetProps(("DefaultValue", items[2]));
        Assert.Single(component.Logger.Messages);

        component.SetProps(("DefaultValue", items[0]));
        Assert.Single(component.Logger.Messages);
    }

    [Fact(DisplayName = "should not fail on null values")]
    [Trait("Requirement", "EVD-EDGE-02")]
    [Trait("Requirement", "EVD-EDGE-03")]
    public void Should_not_fail_on_null_values()
    {
        // JavaScript's undefined is a parameter that is not supplied.
        TestComponent<object?> component = new();
        component.SetProps(("DefaultValue", null));
        Assert.Empty(component.Logger.Messages);

        component.SetProps();
        Assert.Equal([DefaultChanged], component.Logger.Messages);

        component.SetProps(("DefaultValue", null));
        Assert.Single(component.Logger.Messages);
    }

    [Fact(DisplayName = "warnings are written only while the development switch is on")]
    [Trait("Requirement", "EVD-EDGE-01")]
    public void Warnings_are_written_only_while_the_development_switch_is_on()
    {
        TestComponent<object?> component = new();
        component.SetProps();
        try
        {
            AppContext.SetSwitch("Blazix.Primitives.DevelopmentWarnings", false);
            component.SetProps(("Value", 1));
        }
        finally
        {
            AppContext.SetSwitch("Blazix.Primitives.DevelopmentWarnings", true);
        }

        Assert.Empty(component.Logger.Messages);
    }

    [Fact(DisplayName = "the setter identity is the helper's for its lifetime")]
    [Trait("Requirement", "UTL-STA-06")]
    public void The_setter_identity_is_the_helper_s_for_its_lifetime()
    {
        TestComponent<object?> component = new();
        component.SetProps(("DefaultValue", 0));
        Action<object?> setter = component.State.SetValueIfUncontrolled;

        component.SetProps(("DefaultValue", 0));
        setter(5);

        Assert.Equal(5, component.Value);
    }

    [Fact(DisplayName = "later default changes never change the uncontrolled value")]
    [Trait("Requirement", "UTL-STA-05")]
    public void Later_default_changes_never_change_the_uncontrolled_value()
    {
        TestComponent<object?> component = new();
        component.SetProps(("DefaultValue", 0));

        component.SetProps(("DefaultValue", 1));

        Assert.Equal(0, component.Value);
    }

    // The request sequence of ADR 05 §2 (no Base UI title: Base UI tests it through each component at L2).

    [Fact(DisplayName = "a request runs OnValueChange, then ValueChanged, then commits when uncontrolled")]
    [Trait("Requirement", "EVD-ALG-03")]
    [Trait("Requirement", "EVD-STA-08")]
    public async Task A_request_runs_OnValueChange_then_ValueChanged_then_commits_when_uncontrolled()
    {
        TestComponent<bool> component = new();
        component.SetProps(("DefaultValue", false));
        List<string> log = [];
        ChangeEventDetails details = new(EventReason.None);

        bool applied = await component.State.RequestAsync(
            true,
            details,
            Callback<ValueChangeEventArgs<bool>>(args => log.Add($"OnValueChange({args.Value}, committed {component.Value})")),
            Callback<bool>(value => log.Add($"ValueChanged({value}, committed {component.Value})")));

        Assert.True(applied);
        Assert.Equal(["OnValueChange(True, committed False)", "ValueChanged(True, committed False)"], log);
        Assert.True(component.Value);
    }

    [Fact(DisplayName = "a canceled request neither fires ValueChanged nor commits")]
    [Trait("Requirement", "EVD-ALG-03")]
    [Trait("Requirement", "EVD-EVT-05")]
    public async Task A_canceled_request_neither_fires_ValueChanged_nor_commits()
    {
        TestComponent<bool> component = new();
        component.SetProps(("DefaultValue", false));
        int valueChanged = 0;

        bool applied = await component.State.RequestAsync(
            true,
            new ChangeEventDetails(EventReason.None),
            Callback<ValueChangeEventArgs<bool>>(args => args.Details.Cancel()),
            Callback<bool>(_ => valueChanged++));

        Assert.False(applied);
        Assert.Equal(0, valueChanged);
        Assert.False(component.Value);
    }

    [Fact(DisplayName = "a controlled request fires both callbacks and never commits")]
    [Trait("Requirement", "EVD-STA-04")]
    public async Task A_controlled_request_fires_both_callbacks_and_never_commits()
    {
        TestComponent<bool> component = new();
        component.SetProps(("Value", false));
        bool? bound = null;

        bool applied = await component.State.RequestAsync(
            true,
            new ChangeEventDetails(EventReason.None),
            default,
            Callback<bool>(value => bound = value));

        Assert.True(applied);
        Assert.True(bound);
        Assert.False(component.Value);

        component.SetProps(("Value", true));
        Assert.True(component.Value);
    }

    [Fact(DisplayName = "onValueChange cancel() prevents opening while controlled")]
    [Trait("Requirement", "EVD-EVT-06")]
    [Trait("Requirement", "EVD-API-06")]
    public async Task OnValueChange_cancel_prevents_opening_while_controlled()
    {
        TestComponent<bool> component = new();
        component.SetProps(("Value", false));
        bool? canceledInsideCallback = null;
        int valueChanged = 0;

        bool applied = await component.State.RequestAsync(
            true,
            new ChangeEventDetails(EventReason.TriggerPress),
            Callback<ValueChangeEventArgs<bool>>(args =>
            {
                args.Details.Cancel();
                canceledInsideCallback = args.Details.IsCanceled;
            }),
            Callback<bool>(_ => valueChanged++));

        Assert.False(applied);
        Assert.True(canceledInsideCallback);
        Assert.Equal(0, valueChanged);
        Assert.False(component.Value);
    }

    [Fact(DisplayName = "canceling in a grouped Toggle prevents the group value from changing")]
    [Trait("Requirement", "EVD-EVT-04")]
    [Trait("Requirement", "EVD-EVT-09")]
    public async Task Canceling_in_a_grouped_Toggle_prevents_the_group_value_from_changing()
    {
        TestComponent<bool> toggle = new("Toggle");
        toggle.SetProps(("DefaultValue", false));
        TestComponent<string[]> group = new("ToggleGroup");
        group.SetProps(("DefaultValue", Array.Empty<string>()));
        int groupCalls = 0;

        bool applied = await PressGroupedToggle(
            toggle,
            group,
            Callback<ValueChangeEventArgs<bool>>(args => args.Details.Cancel()),
            Callback<ValueChangeEventArgs<string[]>>(_ => groupCalls++));

        Assert.False(applied);
        Assert.Equal(0, groupCalls);
        Assert.Empty(group.Value);
        Assert.False(toggle.Value);
    }

    [Fact(DisplayName = "one details object flows from the child callback to the parent callback")]
    [Trait("Requirement", "EVD-EVT-04")]
    public async Task One_details_object_flows_from_the_child_callback_to_the_parent_callback()
    {
        TestComponent<bool> toggle = new("Toggle");
        toggle.SetProps(("DefaultValue", false));
        TestComponent<string[]> group = new("ToggleGroup");
        group.SetProps(("DefaultValue", Array.Empty<string>()));
        List<(string Callback, ChangeEventDetails Details)> calls = [];

        bool applied = await PressGroupedToggle(
            toggle,
            group,
            Callback<ValueChangeEventArgs<bool>>(args => calls.Add(("toggle", args.Details))),
            Callback<ValueChangeEventArgs<string[]>>(args => calls.Add(("group", args.Details))));

        Assert.True(applied);
        Assert.Equal(["toggle", "group"], calls.Select(call => call.Callback));
        Assert.Same(calls[0].Details, calls[1].Details);
        Assert.True(toggle.Value);
        Assert.Equal(["a"], group.Value);
    }

    [Fact(DisplayName = "canceling in the parent keeps the child uncommitted")]
    [Trait("Requirement", "EVD-ALG-03")]
    public async Task Canceling_in_the_parent_keeps_the_child_uncommitted()
    {
        TestComponent<bool> toggle = new("Toggle");
        toggle.SetProps(("DefaultValue", false));
        TestComponent<string[]> group = new("ToggleGroup");
        group.SetProps(("DefaultValue", Array.Empty<string>()));

        bool applied = await PressGroupedToggle(
            toggle,
            group,
            default,
            Callback<ValueChangeEventArgs<string[]>>(args => args.Details.Cancel()));

        Assert.False(applied);
        Assert.False(toggle.Value);
        Assert.Empty(group.Value);
    }

    [Fact(DisplayName = "an uncontrolled native text edit commits even when canceled, and reports not applied")]
    [Trait("Requirement", "ADR05-A1-2")]
    public async Task An_uncontrolled_native_text_edit_commits_even_when_canceled_and_reports_not_applied()
    {
        TestComponent<string> component = new("Input");
        component.SetProps(("DefaultValue", ""));
        string? bound = null;

        bool applied = await component.State.RequestAsync(
            "typed",
            new ChangeEventDetails(EventReason.InputChange),
            Callback<ValueChangeEventArgs<string>>(args => args.Details.Cancel()),
            Callback<string>(value => bound = value),
            nativeTextEdit: true);

        Assert.False(applied);
        Assert.Equal("typed", bound);
        Assert.Equal("typed", component.Value);
    }

    [Fact(DisplayName = "a controlled native text edit that is canceled does not commit")]
    [Trait("Requirement", "ADR05-A1-2")]
    public async Task A_controlled_native_text_edit_that_is_canceled_does_not_commit()
    {
        TestComponent<string> component = new("Input");
        component.SetProps(("Value", ""));
        int valueChanged = 0;

        bool applied = await component.State.RequestAsync(
            "typed",
            new ChangeEventDetails(EventReason.InputChange),
            Callback<ValueChangeEventArgs<string>>(args => args.Details.Cancel()),
            Callback<string>(_ => valueChanged++),
            nativeTextEdit: true);

        Assert.False(applied);
        Assert.Equal(0, valueChanged);
        Assert.Equal("", component.Value);
    }

    [Fact(DisplayName = "the native text edit option belongs to one request, not to the helper")]
    [Trait("Requirement", "ADR05-A1-2")]
    public async Task The_native_text_edit_option_belongs_to_one_request_not_to_the_helper()
    {
        TestComponent<string> component = new("Input");
        component.SetProps(("DefaultValue", ""));
        EventCallback<ValueChangeEventArgs<string>> cancel = Callback<ValueChangeEventArgs<string>>(args => args.Details.Cancel());

        await component.State.RequestAsync("typed", new ChangeEventDetails(EventReason.InputChange), cancel, default, nativeTextEdit: true);
        await component.State.RequestAsync("cleared", new ChangeEventDetails(EventReason.ClearPress), cancel, default);

        Assert.Equal("typed", component.Value);
    }

    /// <summary>Toggle.tsx's press inside a group: the toggle's callback, the group's with the same details, then both commits.</summary>
    private static async Task<bool> PressGroupedToggle(
        TestComponent<bool> toggle,
        TestComponent<string[]> group,
        EventCallback<ValueChangeEventArgs<bool>> onPressedChange,
        EventCallback<ValueChangeEventArgs<string[]>> onGroupValueChange)
    {
        ChangeEventDetails details = new(EventReason.None);
        if (!await toggle.State.NotifyAsync(true, details, onPressedChange))
        {
            return false;
        }

        if (!await group.State.RequestAsync(["a"], details, onGroupValueChange, default))
        {
            return false;
        }

        await toggle.State.CommitAsync(true, default);
        return true;
    }

    private static EventCallback<T> Callback<T>(Action<T> callback) => new(null, callback);

    /// <summary>A default holding a delegate, as Base UI's <c>{ value: fn }</c> and <c>{ item: &lt;span /&gt; }</c>.</summary>
    public sealed record Holder(Delegate Value);

    /// <summary>The <c>TestComponent</c> of the Base UI file: <c>Value</c>/<c>DefaultValue</c> parameters through <see cref="Controlled{T}"/>.</summary>
    private sealed class TestComponent<T>
    {
        public TestComponent(string name = "TestComponent")
        {
            State = new Controlled<T>(name, "value", Logger);
        }

        public CapturingLogger Logger { get; } = new();

        public Controlled<T> State { get; }

        public T Value => State.Value;

        public void SetProps(params (string Name, object? Value)[] parameters)
        {
            State.SetParameters(
                ParameterView.FromDictionary(parameters.ToDictionary(parameter => parameter.Name, parameter => parameter.Value)),
                "Value",
                "DefaultValue");
        }
    }
}
