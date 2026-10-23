using System.Text.Json;
using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// RadioGroup.test.tsx, the [L2] rows that render neither Field, Fieldset nor Form (issue 84), with the Radio-owned rows of
// radio.md §13, and the twin contract of the group's composite root. A selection reaches C# as a radio's bridge report
// (ToggleAsync, Server) or toggle decision (DecideToggleAsync, WebAssembly); the browser halves are
// tests/browser/radio-group.spec.ts. Port titles are marked as such.
/// <summary>The radio group: its element, its value, its requests and its composite twin.</summary>
public sealed class RadioGroupTests : IDisposable
{
    private readonly RadioTestContext test = new();

    private BunitContext Context => test.Context;

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "can override the built-in attributes")]
    [Trait("Requirement", "RDG-DOM-06")]
    [Trait("Requirement", "RDG-ARIA-01")]
    public void Can_override_the_built_in_attributes()
    {
        IRenderedComponent<RadioGroup<string>> cut = Context.Render<RadioGroup<string>>(p => p.AddUnmatched("role", "switch"));

        Assert.Equal("switch", cut.Nodes.OfType<IElement>().First().GetAttribute("role"));
    }

    [Fact(DisplayName = "is forwarded to the root element")]
    [Trait("Requirement", "RDG-API-10")]
    [Trait("Requirement", "RDG-DOM-01")]
    public void Is_forwarded_to_the_root_element()
    {
        IRenderedComponent<RadioGroup<string>> cut = Context.Render<RadioGroup<string>>(p => p.AddUnmatched("id", "group-id"));

        Assert.Equal("group-id", cut.Find("[role=radiogroup]").GetAttribute("id"));
    }

    [Fact(DisplayName = "port: without an id the group renders none")]
    [Trait("Requirement", "RDG-DOM-01")]
    [Trait("Requirement", "RDG-API-10")]
    [Trait("Requirement", "RDG-DOM-05")]
    public void Port_without_an_id_the_group_renders_none()
    {
        IRenderedComponent<RadioGroup<string>> cut = Context.Render<RadioGroup<string>>(p => p
            .Add(x => x.Name, "fruit")
            .Add(x => x.Form, "f")
            .AddChildContent(RadioTestContext.StringRadios("a")));

        IElement group = cut.Find("[role=radiogroup]");
        foreach (string attribute in new[] { "id", "tabindex", "aria-orientation", "name", "value", "form" })
        {
            Assert.False(group.HasAttribute(attribute), attribute);
        }
    }

    [Fact(DisplayName = "does not forward `value` prop")]
    [Trait("Requirement", "RDG-API-12")]
    [Trait("Requirement", "RDG-DOM-05")]
    public void Does_not_forward_value_prop()
    {
        IRenderedComponent<RadioGroup<string>> cut = Context.Render<RadioGroup<string>>(p => p
            .Add(x => x.Value, "test")
            .AddUnmatched("data-testid", "radio-group")
            .AddChildContent(RadioTestContext.StringRadios(string.Empty)));

        Assert.False(cut.Find("[data-testid=radio-group]").HasAttribute("value"));
    }

    [Fact(DisplayName = "should have the `aria-disabled` attribute")]
    [Trait("Requirement", "RDG-ARIA-03")]
    [Trait("Requirement", "RDG-API-04")]
    [Trait("Requirement", "RAD-ARIA-06")]
    [Trait("Requirement", "RAD-DOM-09")]
    [Trait("Requirement", "RAD-API-02")]
    [Trait("Requirement", "RAD-DOM-04")]
    public void Should_have_the_aria_disabled_attribute()
    {
        IRenderedComponent<RadioGroup<string>> cut = Context.Render<RadioGroup<string>>(p => p
            .Add(x => x.Disabled, true)
            .AddChildContent(RadioTestContext.StringRadios("a")));

        Assert.Equal("true", cut.Find("[role=radiogroup]").GetAttribute("aria-disabled"));
        Assert.Equal("true", cut.Find("[role=radio]").GetAttribute("aria-disabled"));
        Assert.True(cut.Find("[role=radio]").HasAttribute("data-disabled"));
        Assert.True(cut.Find("input[type=radio]").HasAttribute("disabled"));
    }

    [Fact(DisplayName = "should not have the aria attribute when `disabled` is not set")]
    [Trait("Requirement", "RDG-ARIA-03")]
    public void Should_not_have_the_aria_attribute_when_disabled_is_not_set()
    {
        IRenderedComponent<RadioGroup<string>> cut = Context.Render<RadioGroup<string>>();

        Assert.False(cut.Find("[role=radiogroup]").HasAttribute("aria-disabled"));
    }

    [Fact(DisplayName = "should not change its state when clicked (disabled)")]
    [Trait("Requirement", "RDG-EVT-07")]
    [Trait("Requirement", "RAD-PTR-08")]
    [Trait("Requirement", "RDG-API-04")]
    public async Task Should_not_change_its_state_when_clicked_disabled()
    {
        List<string> calls = [];
        IRenderedComponent<RadioGroup<string>> cut = Context.Render<RadioGroup<string>>(p => p
            .Add(x => x.Disabled, true)
            .Add(x => x.OnValueChange, args => calls.Add(args.Value))
            .AddChildContent(RadioTestContext.StringRadios(string.Empty)));

        Assert.Equal("false", cut.Find("[role=radio]").GetAttribute("aria-checked"));
        Assert.Contains("\"disabled\":true", test.ButtonAttachOptions(), StringComparison.Ordinal);

        // A disabled input is never activated; a report that still arrived is refused.
        await test.ToggleAsync(cut, 0);

        Assert.Equal("false", cut.Find("[role=radio]").GetAttribute("aria-checked"));
        Assert.Empty(calls);
    }

    [Fact(DisplayName = "should have the `aria-readonly` attribute")]
    [Trait("Requirement", "RDG-ARIA-04")]
    [Trait("Requirement", "RDG-API-05")]
    public void Should_have_the_aria_readonly_attribute()
    {
        IRenderedComponent<RadioGroup<string>> cut = Context.Render<RadioGroup<string>>(p => p.Add(x => x.ReadOnly, true));

        Assert.Equal("true", cut.Find("[role=radiogroup]").GetAttribute("aria-readonly"));
    }

    [Fact(DisplayName = "should not have the aria attribute when `readOnly` is not set")]
    [Trait("Requirement", "RDG-ARIA-04")]
    public void Should_not_have_the_aria_attribute_when_readOnly_is_not_set()
    {
        IRenderedComponent<RadioGroup<string>> cut = Context.Render<RadioGroup<string>>();

        Assert.False(cut.Find("[role=radiogroup]").HasAttribute("aria-readonly"));
    }

    [Fact(DisplayName = "should not change its state when clicked (readOnly)")]
    [Trait("Requirement", "RDG-EVT-07")]
    [Trait("Requirement", "RAD-PTR-07")]
    [Trait("Requirement", "RDG-EDGE-04")]
    public async Task Should_not_change_its_state_when_clicked_read_only()
    {
        List<string> calls = [];
        IRenderedComponent<RadioGroup<string>> cut = Context.Render<RadioGroup<string>>(p => p
            .Add(x => x.ReadOnly, true)
            .Add(x => x.OnValueChange, args => calls.Add(args.Value))
            .AddChildContent(RadioTestContext.StringRadios(string.Empty)));

        Assert.Equal("false", cut.Find("[role=radio]").GetAttribute("aria-checked"));
        Assert.Contains("\"readOnly\":true", test.ButtonAttachOptions(), StringComparison.Ordinal);

        // The module reverts the input itself; a report that still arrived is refused.
        await test.ToggleAsync(cut, 0);

        Assert.Equal("false", cut.Find("[role=radio]").GetAttribute("aria-checked"));
        Assert.Empty(calls);
    }

    [Fact(DisplayName = "should call onValueChange when an item is clicked")]
    [Trait("Requirement", "RDG-EVT-01")]
    [Trait("Requirement", "RDG-EVT-06")]
    [Trait("Requirement", "RDG-API-03")]
    [Trait("Requirement", "RAD-EVT-01")]
    public async Task Should_call_onValueChange_when_an_item_is_clicked()
    {
        List<string> calls = [];
        List<string?> checkedWhenCalled = [];
        IRenderedComponent<RadioGroup<string>> cut = null!;
        cut = Context.Render<RadioGroup<string>>(p => p
            .Add(x => x.OnValueChange, args =>
            {
                calls.Add(args.Value);
                checkedWhenCalled.Add(cut.Find("[data-testid=a]").GetAttribute("aria-checked"));
            })
            .AddChildContent(RadioTestContext.StringRadios("a")));

        await test.ToggleAsync(cut, 0);

        Assert.Equal(["a"], calls);
        // RDG-EVT-01: before the commit.
        Assert.Equal(["false"], checkedWhenCalled);
        Assert.Equal("true", cut.Find("[data-testid=a]").GetAttribute("aria-checked"));
    }

    [Fact(DisplayName = "should update its state if the underlying input is toggled")]
    [Trait("Requirement", "RDG-STA-02")]
    [Trait("Requirement", "RAD-PTR-04")]
    [Trait("Requirement", "RAD-ARIA-02")]
    public async Task Should_update_its_state_if_the_underlying_input_is_toggled()
    {
        IRenderedComponent<RadioGroup<string>> cut = Context.Render<RadioGroup<string>>(p => p
            .AddUnmatched("data-testid", "root")
            .AddChildContent(RadioTestContext.StringRadios(string.Empty)));

        // The input's own click is reported like a root click: on WebAssembly decided inside it.
        Assert.Equal("run", await test.DecideToggleAsync(cut, 0));

        Assert.Equal("true", cut.Find("[role=radio]").GetAttribute("aria-checked"));
    }

    [Fact(DisplayName = "should place the style hooks on the root and subcomponents")]
    [Trait("Requirement", "RDG-DOM-02")]
    [Trait("Requirement", "RDG-STA-01")]
    [Trait("Requirement", "RAD-DOM-03")]
    [Trait("Requirement", "RAD-DOM-04")]
    [Trait("Requirement", "RAD-DOM-15")]
    [Trait("Requirement", "RAD-STA-03")]
    [Trait("Requirement", "RAD-STA-04")]
    [Trait("Requirement", "RAD-API-04")]
    [Trait("Requirement", "RDG-API-06")]
    public void Should_place_the_style_hooks_on_the_root_and_subcomponents()
    {
        RadioRootState? rootState = null;
        RadioIndicatorState? indicatorState = null;
        RadioGroupState? groupState = null;
        IRenderedComponent<RadioGroup<string>> cut = Context.Render<RadioGroup<string>>(p => p
            .Add(x => x.DefaultValue, "1")
            .Add(x => x.Disabled, true)
            .Add(x => x.ReadOnly, true)
            .Add(x => x.Required, true)
            .Add(x => x.ClassValue, state =>
            {
                groupState = state;
                return null;
            })
            .AddChildContent<RadioRoot<string>>(r => r
                .Add(x => x.Value, "1")
                .AddUnmatched("data-testid", "item")
                .Add(x => x.ClassValue, state =>
                {
                    rootState = state;
                    return null;
                })
                .AddChildContent<RadioIndicator>(i => i
                    .AddUnmatched("data-testid", "indicator")
                    .Add(x => x.ClassValue, state =>
                    {
                        indicatorState = state;
                        return null;
                    }))));

        IElement root = cut.Find("[role=radiogroup]");
        IElement item = cut.Find("[data-testid=item]");
        IElement indicator = cut.Find("[data-testid=indicator]");

        Assert.Equal(string.Empty, root.GetAttribute("data-disabled"));
        Assert.Equal(string.Empty, root.GetAttribute("data-readonly"));
        Assert.Equal(string.Empty, root.GetAttribute("data-required"));

        foreach (IElement element in new[] { item, indicator })
        {
            Assert.Equal(string.Empty, element.GetAttribute("data-checked"));
            Assert.Equal(string.Empty, element.GetAttribute("data-disabled"));
            Assert.Equal(string.Empty, element.GetAttribute("data-readonly"));
            Assert.Equal(string.Empty, element.GetAttribute("data-required"));
        }

        // RAD-STA-03, RDG-STA-01: the effective values in exactly the documented fields; RAD-STA-04: the same in the indicator.
        Assert.Equal(new RadioRootState(true, true, true, true, false, false, null, false, false), rootState);
        Assert.Equal(new RadioIndicatorState(true, true, true, true, false, false, null, false, false, null), indicatorState);
        Assert.Equal(new RadioGroupState(true, true, true, false, false, null, false, false), groupState);

        // RAD-API-04: required reaches the input only, never aria-required on the radio.
        Assert.True(cut.Find("input[type=radio]").HasAttribute("required"));
        Assert.False(item.HasAttribute("aria-required"));
        Assert.Equal("true", root.GetAttribute("aria-required"));
    }

    [Fact(DisplayName = "should set the name attribute on each radio input")]
    [Trait("Requirement", "RDG-API-07")]
    [Trait("Requirement", "RAD-DOM-09")]
    [Trait("Requirement", "RAD-DOM-11")]
    [Trait("Requirement", "RAD-FRM-02")]
    [Trait("Requirement", "RAD-STR-01")]
    [Trait("Requirement", "RDG-API-08")]
    public void Should_set_the_name_attribute_on_each_radio_input()
    {
        IRenderedComponent<RadioGroup<string>> cut = Context.Render<RadioGroup<string>>(p => p
            .Add(x => x.Name, "radio-group")
            .Add(x => x.Form, "external-form")
            .AddChildContent(RadioTestContext.StringRadios("a")));

        IElement? input = cut.Find("[data-testid=a]").NextElementSibling;
        Assert.Equal("INPUT", input?.TagName);
        Assert.Equal("radio-group", input!.GetAttribute("name"));
        Assert.Equal("a", input.GetAttribute("value"));
        Assert.Equal("external-form", input.GetAttribute("form"));
    }

    [Fact(DisplayName = "should apply data-checked and data-unchecked to radio root and indicator")]
    [Trait("Requirement", "RAD-DOM-03")]
    [Trait("Requirement", "RAD-DOM-15")]
    [Trait("Requirement", "RAD-STA-04")]
    [Trait("Requirement", "RAD-API-10")]
    [Trait("Requirement", "RDG-PTR-01")]
    [Trait("Requirement", "RAD-ANI-04")]
    public async Task Should_apply_data_checked_and_data_unchecked_to_radio_root_and_indicator()
    {
        IRenderedComponent<RadioGroup<string>> cut = Context.Render<RadioGroup<string>>(p => p.AddChildContent(builder =>
        {
            foreach ((string value, int sequence) in new[] { ("a", 0), ("b", 10) })
            {
                builder.OpenComponent<RadioRoot<string>>(sequence);
                builder.AddComponentParameter(sequence + 1, nameof(RadioRoot<string>.Value), value);
                builder.AddAttribute(sequence + 2, "data-testid", value);
                builder.AddComponentParameter(sequence + 3, "ChildContent", (RenderFragment)(content =>
                {
                    content.OpenComponent<RadioIndicator>(0);
                    content.AddComponentParameter(1, nameof(RadioIndicator.KeepMounted), true);
                    content.AddAttribute(2, "data-testid", "indicator-" + value);
                    content.CloseComponent();
                }));
                builder.CloseComponent();
            }
        }));

        AssertChecked(cut, a: false, b: false);
        await test.ToggleAsync(cut, 0);
        AssertChecked(cut, a: true, b: false);
        await test.ToggleAsync(cut, 1);
        AssertChecked(cut, a: false, b: true);
        await test.ToggleAsync(cut, 0);
        AssertChecked(cut, a: true, b: false);

        static void AssertChecked(IRenderedComponent<RadioGroup<string>> cut, bool a, bool b)
        {
            foreach ((string id, bool expected) in new[] { ("a", a), ("indicator-a", a), ("b", b), ("indicator-b", b) })
            {
                IElement element = cut.Find($"[data-testid={id}]");
                Assert.Equal(expected, element.HasAttribute("data-checked"));
                Assert.Equal(!expected, element.HasAttribute("data-unchecked"));
            }
        }
    }

    // RadioIndicator.test.tsx "should remove the indicator when there is no exit animation defined" (RDG-EDGE-02, L2 half):
    // two radios with one value are both checked, and each indicator leaves on its own completion report.
    [Fact(DisplayName = "should remove the indicator when there is no exit animation defined")]
    [Trait("Requirement", "RDG-EDGE-02")]
    [Trait("Requirement", "RAD-EDGE-10")]
    [Trait("Requirement", "RAD-ANI-01")]
    public async Task Should_remove_the_indicator_when_there_is_no_exit_animation_defined()
    {
        IRenderedComponent<RadioGroup<string>> cut = Context.Render<RadioGroup<string>>(p => p
            .Add(x => x.Value, "a")
            .AddChildContent(builder =>
            {
                for (int i = 0; i < 2; i++)
                {
                    builder.OpenComponent<RadioRoot<string>>(i * 10);
                    builder.AddComponentParameter((i * 10) + 1, nameof(RadioRoot<string>.Value), "a");
                    builder.AddComponentParameter((i * 10) + 2, "ChildContent", (RenderFragment)(content =>
                    {
                        content.OpenComponent<RadioIndicator>(0);
                        content.AddAttribute(1, "data-indicator", string.Empty);
                        content.CloseComponent();
                    }));
                    builder.CloseComponent();
                }
            }));

        Assert.Equal(2, cut.FindAll("[role=radio][aria-checked=true]").Count);
        Assert.Equal(2, cut.FindAll("[data-composite-item-active]").Count);
        Assert.Equal(2, cut.FindAll("[data-indicator]").Count);

        cut.Render(p => p.Add(x => x.Value, "b"));
        Assert.Equal(2, cut.FindAll("[data-indicator][data-unchecked]").Count);

        // Each indicator has its own twin; the module reports each completion.
        foreach (Blazix.Primitives.Interop.TransitionsTwin twin in test.TransitionsModule.Invocations["attach"]
            .Select(i => ((Microsoft.JSInterop.DotNetObjectReference<Blazix.Primitives.Interop.TransitionsTwin>)i.Arguments[2]!).Value)
            .ToArray())
        {
            await cut.InvokeAsync(() => twin.OnTransitionComplete("""{"open":false}"""));
        }

        Assert.Empty(cut.FindAll("[data-indicator]"));
    }

    [Fact(DisplayName = "does not change state when canceled via a root click")]
    [Trait("Requirement", "RDG-EVT-04")]
    [Trait("Requirement", "RDG-STA-05")]
    [Trait("Requirement", "RAD-STA-05")]
    public async Task Does_not_change_state_when_canceled_via_a_root_click()
    {
        IRenderedComponent<FieldRoot> cut = RenderCanceledField("a");

        // WebAssembly: the decision inside the dispatched input click answers stop, so the browser reverts the input.
        Assert.Equal("stop", await test.DecideToggleAsync(cut, 0));
        AssertUnchanged(cut);
    }

    [Fact(DisplayName = "does not change state when canceled via a hidden input click")]
    [Trait("Requirement", "RDG-EVT-04")]
    [Trait("Requirement", "RDG-STA-05")]
    [Trait("Requirement", "RAD-PTR-04")]
    public async Task Does_not_change_state_when_canceled_via_a_hidden_input_click()
    {
        IRenderedComponent<FieldRoot> cut = RenderCanceledField("a");

        // Server: the report of the input's own click is refused, and the input restored through the dom primitive.
        await test.ToggleAsync(cut, 0);
        AssertUnchanged(cut);
        Assert.Equal("""{"checked":false,"checkedRestores":1,"liveRegion":false}""", test.DomUpdates[^1]);
    }

    [Fact(DisplayName = "does not change state when canceled via arrow key navigation")]
    [Trait("Requirement", "RDG-EVT-04")]
    [Trait("Requirement", "RDG-KEY-03")]
    public async Task Does_not_change_state_when_canceled_via_arrow_key_navigation()
    {
        IRenderedComponent<FieldRoot> cut = RenderCanceledField("a", "b");

        // The focus-driven selection clicks b's input: the request is canceled; the focus move is the module's (L4).
        Assert.Equal("run", await test.DecideSelectAsync(cut, RadioTestContext.Radios(cut)[1].Id!));
        Assert.Equal("stop", await test.DecideToggleAsync(cut, 1));
        AssertUnchanged(cut);
    }

    // Design point R2 (c), pending the user: on Server the browser may have unchecked the selected same-name radio natively,
    // so a refused click restores the clicked input and the selected one, each with one dom update and no other render.
    [Fact(DisplayName = "port: a refused selection restores the clicked input and the selected one on Server")]
    [Trait("Requirement", "RDG-EVT-04")]
    [Trait("Requirement", "RDG-STA-03")]
    public async Task Port_a_refused_selection_restores_the_clicked_input_and_the_selected_one()
    {
        List<string> renders = [];
        IRenderedComponent<RadioGroup<string>> cut = Context.Render<RadioGroup<string>>(p => p
            .Add(x => x.Name, "fruit")
            .Add(x => x.Value, "a")
            .AddChildContent(builder =>
            {
                foreach ((string value, int sequence) in new[] { ("a", 0), ("b", 10), ("c", 20) })
                {
                    builder.OpenComponent<RadioRoot<string>>(sequence);
                    builder.AddComponentParameter(sequence + 1, nameof(RadioRoot<string>.Value), value);
                    builder.AddComponentParameter(sequence + 2, nameof(RadioRoot<string>.ClassValue), (Func<RadioRootState, string?>)(_ =>
                    {
                        renders.Add(value);
                        return null;
                    }));
                    builder.CloseComponent();
                }
            }));
        int updates = test.DomUpdates.Count;
        renders.Clear();

        // A controlled group whose consumer does not pass the value back.
        await test.ToggleAsync(cut, 1);

        Assert.Equal(
            ["""{"checked":false,"checkedRestores":1,"liveRegion":false}""", """{"checked":true,"checkedRestores":1,"liveRegion":false}"""],
            test.DomUpdates.Skip(updates).Order(StringComparer.Ordinal));
        Assert.Empty(renders);
        Assert.Equal(["true", "false", "false"], RadioTestContext.Radios(cut).Select(r => r.GetAttribute("aria-checked")));
    }

    [Fact(DisplayName = "port: the value is reported once with the reason none, and allowPropagation changes nothing")]
    [Trait("Requirement", "RDG-EVT-02")]
    [Trait("Requirement", "RDG-EVT-08")]
    [Trait("Requirement", "RDG-EVT-05")]
    public async Task Port_the_value_is_reported_once_with_the_reason_none()
    {
        List<ValueChangeEventArgs<string>> calls = [];
        IRenderedComponent<RadioGroup<string>> cut = Context.Render<RadioGroup<string>>(p => p
            .Add(x => x.DefaultValue, "a")
            .Add(x => x.OnValueChange, args =>
            {
                args.Details.AllowPropagation();
                calls.Add(args);
            })
            .AddChildContent(RadioTestContext.StringRadios("a", "b")));

        // RDG-EVT-05: not at mount, nor for a parameter change.
        cut.Render(p => p.Add(x => x.Disabled, false));
        Assert.Empty(calls);

        await test.ToggleAsync(cut, 1);

        ValueChangeEventArgs<string> call = Assert.Single(calls);
        Assert.Equal(EventReason.None, call.Details.Reason);
        Assert.True(call.Details.IsPropagationAllowed);
        Assert.Equal("true", RadioTestContext.Radios(cut)[1].GetAttribute("aria-checked"));
    }

    [Fact(DisplayName = "port: a controlled group renders the value it is given and only requests a change")]
    [Trait("Requirement", "RDG-API-01")]
    [Trait("Requirement", "RDG-STA-03")]
    [Trait("Requirement", "RDG-STA-04")]
    public async Task Port_a_controlled_group_renders_the_value_it_is_given()
    {
        List<string> requested = [];
        IRenderedComponent<RadioGroup<string>> cut = Context.Render<RadioGroup<string>>(p => p
            .Add(x => x.Value, "a")
            .Add(x => x.DefaultValue, "c")
            .Add(x => x.ValueChanged, value => requested.Add(value))
            .AddChildContent(RadioTestContext.StringRadios("a", "b", "c")));

        await test.ToggleAsync(cut, 1);
        Assert.Equal(["b"], requested);
        Assert.Equal(["true", "false", "false"], RadioTestContext.Radios(cut).Select(r => r.GetAttribute("aria-checked")));

        cut.Render(p => p.Add(x => x.Value, "b").Add(x => x.DefaultValue, "c"));
        Assert.Equal(["false", "true", "false"], RadioTestContext.Radios(cut).Select(r => r.GetAttribute("aria-checked")));

        // RDG-STA-04: a controlled value later removed falls back to the initial default (ADR 05 Amendments 2026-10-18).
        cut.Render(p => p.Add(x => x.DefaultValue, "c").AddChildContent(RadioTestContext.StringRadios("a", "b", "c")));
        Assert.Equal(["false", "false", "true"], RadioTestContext.Radios(cut).Select(r => r.GetAttribute("aria-checked")));
    }

    // Design point R1 (b), pending the user: a non-nullable group without a default holds no value until its first
    // selection, so the radio whose value is default(TValue) is not checked (RDG-API-02's `undefined`).
    [Fact(DisplayName = "port: an uncontrolled group of a value type without a default selects nothing")]
    [Trait("Requirement", "RDG-API-02")]
    [Trait("Requirement", "RDG-EDGE-03")]
    public async Task Port_an_uncontrolled_group_of_a_value_type_without_a_default_selects_nothing()
    {
        IRenderedComponent<RadioGroup<int>> cut = Context.Render<RadioGroup<int>>(p => p.AddChildContent(builder =>
        {
            for (int i = 0; i < 2; i++)
            {
                builder.OpenComponent<RadioRoot<int>>(i * 10);
                builder.AddComponentParameter((i * 10) + 1, nameof(RadioRoot<int>.Value), i);
                builder.CloseComponent();
            }
        }));

        Assert.Equal(["false", "false"], RadioTestContext.Radios(cut).Select(r => r.GetAttribute("aria-checked")));
        Assert.Equal(["0", "1"], RadioTestContext.Inputs(cut).Select(i => i.GetAttribute("value")));

        await test.ToggleAsync(cut, 0);
        Assert.Equal(["true", "false"], RadioTestContext.Radios(cut).Select(r => r.GetAttribute("aria-checked")));
    }

    // ADR 08 Amendments 2026-10-22: a null group value checks no radio, a radio whose value is null included (RDG-EDGE-03's
    // dated deviation note).
    [Fact(DisplayName = "port: a null value selects no radio")]
    [Trait("Requirement", "RDG-EDGE-03")]
    [Trait("Requirement", "RDG-API-01")]
    public void Port_a_null_value_selects_no_radio()
    {
        IRenderedComponent<RadioGroup<string?>> cut = Context.Render<RadioGroup<string?>>(p => p
            .Add(x => x.Value, (string?)null)
            .AddChildContent(builder =>
            {
                builder.OpenComponent<RadioRoot<string?>>(0);
                builder.AddComponentParameter(1, nameof(RadioRoot<string?>.Value), (string?)null);
                builder.CloseComponent();
                builder.OpenComponent<RadioRoot<string?>>(2);
                builder.AddComponentParameter(3, nameof(RadioRoot<string?>.Value), "a");
                builder.CloseComponent();
            }));

        Assert.Equal(["false", "false"], RadioTestContext.Radios(cut).Select(r => r.GetAttribute("aria-checked")));
    }

    [Fact(DisplayName = "port: identity decides which radio is checked: references by identity, strings by value, NaN never")]
    [Trait("Requirement", "RAD-STA-01")]
    [Trait("Requirement", "RDG-STA-02")]
    public void Port_identity_decides_which_radio_is_checked()
    {
        Fruit apple = new("apple");
        IRenderedComponent<RadioGroup<Fruit>> records = Context.Render<RadioGroup<Fruit>>(p => p
            .Add(x => x.DefaultValue, apple)
            .AddChildContent(builder =>
            {
                builder.OpenComponent<RadioRoot<Fruit>>(0);
                builder.AddComponentParameter(1, nameof(RadioRoot<Fruit>.Value), new Fruit("apple"));
                builder.CloseComponent();
                builder.OpenComponent<RadioRoot<Fruit>>(2);
                builder.AddComponentParameter(3, nameof(RadioRoot<Fruit>.Value), apple);
                builder.CloseComponent();
            }));
        Assert.Equal(["false", "true"], RadioTestContext.Radios(records).Select(r => r.GetAttribute("aria-checked")));
        Assert.Equal(["Fruit { Name = apple }", "Fruit { Name = apple }"], RadioTestContext.Inputs(records).Select(i => i.GetAttribute("value")));

        IRenderedComponent<RadioGroup<double>> numbers = Context.Render<RadioGroup<double>>(p => p
            .Add(x => x.DefaultValue, double.NaN)
            .AddChildContent(builder =>
            {
                builder.OpenComponent<RadioRoot<double>>(0);
                builder.AddComponentParameter(1, nameof(RadioRoot<double>.Value), double.NaN);
                builder.CloseComponent();
            }));
        Assert.Equal("false", numbers.Find("[role=radio]").GetAttribute("aria-checked"));
    }

    // --- The composite root (twin contract) ---------------------------------------------------------------------------

    [Fact(DisplayName = "port: the group attaches one composite root with RadioGroup's configuration")]
    [Trait("Requirement", "RDG-KEY-01")]
    [Trait("Requirement", "RDG-KEY-02")]
    [Trait("Requirement", "RDG-KEY-05")]
    [Trait("Requirement", "RDG-KEY-04")]
    [Trait("Requirement", "RDG-FOC-02")]
    [Trait("Requirement", "RDG-STA-06")]
    public void Port_the_group_attaches_one_composite_root_with_RadioGroups_configuration()
    {
        IRenderedComponent<RadioGroup<string>> cut = Context.Render<RadioGroup<string>>(p => p.AddChildContent(RadioTestContext.StringRadios("a", "b")));

        string[] ids = [.. RadioTestContext.Radios(cut).Select(r => r.Id!)];
        Assert.Equal(cut.Instance.Element, Assert.Single(test.CompositeModule.Invocations["attach"]).Arguments[0]);
        Assert.Equal(
            $$"""{"orientation":"both","loopFocus":true,"enableHomeAndEndKeys":false,"stopEventPropagation":true,"highlightItemOnHover":false,"modifierKeys":["Shift"],"direction":"ltr","items":[{"id":"{{ids[0]}}"},{"id":"{{ids[1]}}"}],"selectOnNavigation":true,"reportFocusLeave":false,"reportHighlight":false,"reportHandledKeys":false}""",
            test.LastCompositeOptions);
    }

    [Fact(DisplayName = "port: the direction comes from the nearest DirectionProvider and its change is sent")]
    [Trait("Requirement", "RDG-EDGE-05")]
    [Trait("Requirement", "CMP-I18N-01")]
    [Trait("Requirement", "CMP-I18N-02")]
    public void Port_the_direction_comes_from_the_nearest_DirectionProvider()
    {
        IRenderedComponent<DirectionProvider> cut = Context.Render<DirectionProvider>(p => p
            .Add(x => x.Direction, TextDirection.Rtl)
            .AddChildContent<RadioGroup<string>>(g => g.AddChildContent(RadioTestContext.StringRadios("a"))));

        Assert.Equal("rtl", JsonDocument.Parse(test.CompositeAttaches[^1]).RootElement.GetProperty("direction").GetString());

        cut.Render(p => p.Add(x => x.Direction, TextDirection.Ltr));

        Assert.Equal("ltr", JsonDocument.Parse(test.LastCompositeOptions).RootElement.GetProperty("direction").GetString());
    }

    [Fact(DisplayName = "port: a radio added or removed is sent in one update")]
    [Trait("Requirement", "RDG-FOC-03")]
    [Trait("Requirement", "RDG-STR-03")]
    public void Port_a_radio_added_or_removed_is_sent_in_one_update()
    {
        IRenderedComponent<RadioGroup<string>> cut = Context.Render<RadioGroup<string>>(p => p.AddChildContent(RadioTestContext.StringRadios("a", "b", "c")));
        int updates = test.CompositeUpdates.Count;

        cut.Render(p => p.AddChildContent(RadioTestContext.StringRadios("a", "b")));

        Assert.Equal(updates + 1, test.CompositeUpdates.Count);
        Assert.Equal(2, JsonDocument.Parse(test.LastCompositeOptions).RootElement.GetProperty("items").GetArrayLength());
    }

    [Fact(DisplayName = "port: the group's consumer onkeydown is declined and run at the navigate decision")]
    [Trait("Requirement", "RDG-API-12")]
    [Trait("Requirement", "CMP-EVT-12")]
    [Trait("Requirement", "CMP-KEY-15")]
    public async Task Port_the_groups_consumer_onkeydown_is_declined_and_run_at_the_navigate_decision()
    {
        List<string> keys = [];
        IRenderedComponent<RadioGroup<string>> cut = Context.Render<RadioGroup<string>>(p => p
            .AddUnmatched("onkeydown", EventCallback.Factory.Create<KeyboardEventArgs>(this, e =>
            {
                keys.Add(e.Key);
                e.PreventPartHandler();
            }))
            .AddChildContent(RadioTestContext.StringRadios("a", "b")));

        string first = RadioTestContext.Radios(cut)[0].Id!;
        Assert.Equal("stop", await test.DecideNavigateAsync(cut, "ArrowDown", first));
        Assert.Equal(["ArrowDown"], keys);

        // The vetoed key reaches Blazor afterwards; the one-shot marker keeps it from running twice.
        await cut.Find("[role=radiogroup]").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "ArrowDown", Code = "ArrowDown" });
        Assert.Equal(["ArrowDown"], keys);
    }

    private IRenderedComponent<FieldRoot> RenderCanceledField(params string[] values)
    {
        return Context.Render<FieldRoot>(p => p.AddChildContent<RadioGroup<string>>(g => g
            .Add(x => x.OnValueChange, args => args.Details.Cancel())
            .AddChildContent(RadioTestContext.StringRadios(values))));
    }

    private static void AssertUnchanged(IRenderedComponent<FieldRoot> cut)
    {
        IElement group = cut.Find("[role=radiogroup]");
        Assert.All(RadioTestContext.Radios(cut), radio => Assert.Equal("false", radio.GetAttribute("aria-checked")));
        Assert.False(group.HasAttribute("data-touched"));
        Assert.False(group.HasAttribute("data-dirty"));
        Assert.False(group.HasAttribute("data-filled"));
    }

    /// <summary>A record whose instances compare by value in C#, but by identity in a radio group.</summary>
    public sealed record Fruit(string Name);
}
