using AngleSharp.Dom;
using Blazix.Primitives.Rendering;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// CheckboxRoot.test.tsx, the rows that render neither Field, Form nor CheckboxGroup (issue 81). A click reaches C# through
// the `button` module: on Server as the OnToggle report (ToggleAsync), on WebAssembly as the toggle decision
// (DecideToggleAsync); the browser half of each title is tests/browser/checkbox.spec.ts. Port titles are marked as such.
/// <summary>The checkbox root: its element, its sibling inputs, its controlled value, its attributes and its twins.</summary>
public sealed class CheckboxRootTests : IDisposable
{
    private const string CheckboxSelector = "[role=checkbox]";

    private static readonly RenderFragment s_nativeButtonElement = builder =>
    {
        builder.OpenElement(0, "button");
        builder.CloseElement();
    };

    private readonly CheckboxTestContext test = new();

    private BunitContext Context => test.Context;

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "sets the correct aria attributes")]
    [Trait("Requirement", "CHK-ARIA-01")]
    [Trait("Requirement", "CHK-ARIA-02")]
    [Trait("Requirement", "CHK-ARIA-04")]
    [Trait("Requirement", "CHK-API-10")]
    public void Sets_the_correct_aria_attributes()
    {
        IRenderedComponent<CheckboxRoot> cut = Context.Render<CheckboxRoot>(p => p.AddUnmatched("data-testid", "test").Add(x => x.Required, false));

        IElement root = cut.Find(CheckboxSelector);
        Assert.Equal(root.GetAttribute("id"), cut.Find("[data-testid=test]").GetAttribute("id"));
        Assert.True(root.HasAttribute("aria-checked"));
        Assert.False(root.HasAttribute("aria-required"));
        Assert.False(cut.Find("input[type=checkbox]").HasAttribute("required"));

        cut.Render(p => p.Add(x => x.Required, true));

        Assert.Equal("true", cut.Find(CheckboxSelector).GetAttribute("aria-required"));
        Assert.True(cut.Find("input[type=checkbox]").HasAttribute("required"));
    }

    [Fact(DisplayName = "can override the built-in attributes")]
    [Trait("Requirement", "CHK-DOM-09")]
    [Trait("Requirement", "CHK-ARIA-01")]
    [Trait("Requirement", "CHK-API-17")]
    public void Can_override_the_built_in_attributes()
    {
        IRenderedComponent<CheckboxRoot> cut = Context.Render<CheckboxRoot>(p => p.AddUnmatched("role", "switch"));

        Assert.Equal("switch", cut.Nodes.OfType<IElement>().First().GetAttribute("role"));
    }

    [Fact(DisplayName = "should change its state when clicked")]
    [Trait("Requirement", "CHK-STA-04")]
    [Trait("Requirement", "CHK-ARIA-02")]
    [Trait("Requirement", "CHK-DOM-12")]
    [Trait("Requirement", "CHK-API-02")]
    [Trait("Requirement", "CHK-STR-01")]
    public async Task Should_change_its_state_when_clicked()
    {
        IRenderedComponent<CheckboxRoot> cut = Context.Render<CheckboxRoot>();

        // CHK-STR-01: the root, then the checkbox input, as siblings; the input is never inside the root.
        Assert.Equal(["SPAN", "INPUT"], cut.Nodes.OfType<IElement>().Select(e => e.TagName));
        Assert.Empty(cut.Find("span").QuerySelectorAll("input"));
        Assert.Equal("false", cut.Find(CheckboxSelector).GetAttribute("aria-checked"));
        Assert.Equal("""{"indeterminate":false,"checked":false,"liveRegion":false}""", test.DomOptions[^1]);

        Assert.Equal("run", await test.DecideToggleAsync(cut));
        Assert.Equal("true", cut.Find(CheckboxSelector).GetAttribute("aria-checked"));
        Assert.Equal("""{"indeterminate":false,"checked":true,"liveRegion":false}""", test.DomOptions[^1]);

        await test.ToggleAsync(cut, false);
        Assert.Equal("false", cut.Find(CheckboxSelector).GetAttribute("aria-checked"));
        Assert.Equal("""{"indeterminate":false,"checked":false,"liveRegion":false}""", test.DomOptions[^1]);
    }

    [Fact(DisplayName = "should update its state when changed from outside")]
    [Trait("Requirement", "CHK-STA-05")]
    [Trait("Requirement", "CHK-API-01")]
    public async Task Should_update_its_state_when_changed_from_outside()
    {
        IRenderedComponent<CheckboxRoot> cut = Context.Render<CheckboxRoot>(p => p.Add(x => x.Checked, false));
        Assert.Equal("false", cut.Find(CheckboxSelector).GetAttribute("aria-checked"));

        cut.Render(p => p.Add(x => x.Checked, true));
        Assert.Equal("true", cut.Find(CheckboxSelector).GetAttribute("aria-checked"));
        Assert.Equal("""{"indeterminate":false,"checked":true,"liveRegion":false}""", test.DomOptions[^1]);

        // A user toggle alone does not move a controlled box; the module is told to restore the input.
        await test.ToggleAsync(cut, false);
        Assert.Equal("true", cut.Find(CheckboxSelector).GetAttribute("aria-checked"));
        Assert.Equal("""{"indeterminate":false,"checked":true,"checkedRestores":1,"liveRegion":false}""", test.DomOptions[^1]);

        cut.Render(p => p.Add(x => x.Checked, false));
        Assert.Equal("false", cut.Find(CheckboxSelector).GetAttribute("aria-checked"));
    }

    [Fact(DisplayName = "should call onCheckedChange when clicked")]
    [Trait("Requirement", "CHK-EVT-01")]
    [Trait("Requirement", "CHK-EVT-06")]
    [Trait("Requirement", "CHK-API-03")]
    public async Task Should_call_onCheckedChange_when_clicked()
    {
        List<ValueChangeEventArgs<bool>> calls = [];
        List<string?> stateWhenCalled = [];
        IRenderedComponent<CheckboxRoot> cut = null!;
        cut = Context.Render<CheckboxRoot>(p => p.Add(x => x.OnCheckedChange, args =>
        {
            calls.Add(args);
            stateWhenCalled.Add(cut.Find(CheckboxSelector).GetAttribute("aria-checked"));
        }));

        await test.ToggleAsync(cut, true, shiftKey: true);

        ValueChangeEventArgs<bool> call = Assert.Single(calls);
        Assert.True(call.Value);
        Assert.Equal(["false"], stateWhenCalled);
        Assert.Equal("click", call.Details.Event.Type);
        Assert.True(call.Details.Event.ShiftKey);
        Assert.Null(call.Details.Trigger);
    }

    [Fact(DisplayName = "port: the change details carry the reason none, and allowPropagation changes nothing")]
    [Trait("Requirement", "CHK-EVT-02")]
    [Trait("Requirement", "CHK-EVT-08")]
    public async Task Port_the_change_details_carry_the_reason_none()
    {
        List<ChangeEventDetails> details = [];
        IRenderedComponent<CheckboxRoot> cut = Context.Render<CheckboxRoot>(p => p.Add(x => x.OnCheckedChange, args =>
        {
            args.Details.AllowPropagation();
            details.Add(args.Details);
        }));

        await test.ToggleAsync(cut, true);
        await test.DecideToggleAsync(cut);

        Assert.Equal(2, details.Count);
        Assert.All(details, d => Assert.Equal(EventReason.None, d.Reason));
        Assert.All(details, d => Assert.True(d.IsPropagationAllowed));
        Assert.Equal("false", cut.Find(CheckboxSelector).GetAttribute("aria-checked"));
    }

    [Fact(DisplayName = "does not update its state when onCheckedChange cancels the event")]
    [Trait("Requirement", "CHK-EVT-04")]
    [Trait("Requirement", "CHK-STA-05")]
    public async Task Does_not_update_its_state_when_onCheckedChange_cancels_the_event()
    {
        int calls = 0;
        IRenderedComponent<CheckboxRoot> cut = Context.Render<CheckboxRoot>(p => p.Add(x => x.OnCheckedChange, args =>
        {
            calls++;
            args.Details.Cancel();
        }));

        // WebAssembly: the decision answers stop, so the module reverts the box natively.
        Assert.Equal("stop", await test.DecideToggleAsync(cut));
        Assert.Equal("false", cut.Find(CheckboxSelector).GetAttribute("aria-checked"));
        Assert.Equal(["""{"indeterminate":false,"checked":false,"liveRegion":false}"""], test.DomOptions);

        // Server: the report arrives after the browser flipped the box, so the part asks the module to restore it.
        await test.ToggleAsync(cut, true);
        Assert.Equal(2, calls);
        Assert.Equal("false", cut.Find(CheckboxSelector).GetAttribute("aria-checked"));
        Assert.Equal("""{"indeterminate":false,"checked":false,"checkedRestores":1,"liveRegion":false}""", test.DomOptions[^1]);
    }

    [Fact(DisplayName = "port: CheckedChanged runs after OnCheckedChange and not for a canceled toggle")]
    [Trait("Requirement", "CHK-API-03")]
    [Trait("Requirement", "CHK-EVT-04")]
    public async Task Port_CheckedChanged_runs_after_OnCheckedChange_and_not_for_a_canceled_toggle()
    {
        List<string> order = [];
        bool cancel = true;
        IRenderedComponent<CheckboxRoot> cut = Context.Render<CheckboxRoot>(p => p
            .Add(x => x.OnCheckedChange, args =>
            {
                order.Add($"change:{args.Value}");
                if (cancel)
                {
                    args.Details.Cancel();
                }
            })
            .Add(x => x.CheckedChanged, value => order.Add($"bind:{value}")));

        await test.ToggleAsync(cut, true);
        cancel = false;
        await test.ToggleAsync(cut, true);

        Assert.Equal(["change:True", "change:True", "bind:True"], order);
    }

    [Fact(DisplayName = "port: a controlled box whose consumer does not pass the value back answers stop on WebAssembly")]
    [Trait("Requirement", "CHK-STA-05")]
    public async Task Port_a_controlled_box_whose_consumer_does_not_pass_the_value_back_answers_stop()
    {
        List<bool> requested = [];
        IRenderedComponent<CheckboxRoot> cut = Context.Render<CheckboxRoot>(p => p
            .Add(x => x.Checked, false)
            .Add(x => x.CheckedChanged, requested.Add));

        Assert.Equal("stop", await test.DecideToggleAsync(cut));

        Assert.Equal([true], requested);
        Assert.Equal("false", cut.Find(CheckboxSelector).GetAttribute("aria-checked"));
    }

    [Fact(DisplayName = "port: ownership is fixed at the first parameter set, and a removed checked falls back to the initial default")]
    [Trait("Requirement", "CHK-STA-03")]
    [Trait("Requirement", "CHK-STA-06")]
    [Trait("Requirement", "CHK-EDGE-02")]
    public async Task Port_ownership_is_fixed_at_the_first_parameter_set()
    {
        IRenderedComponent<CheckboxRoot> cut = Context.Render<CheckboxRoot>(p => p.Add(x => x.Checked, true).Add(x => x.DefaultChecked, false));

        cut.Render(p => p.Add(x => x.DefaultChecked, false));
        Assert.Equal("false", cut.Find(CheckboxSelector).GetAttribute("aria-checked"));

        // Still controlled: a toggle does not commit.
        await test.ToggleAsync(cut, true);
        Assert.Equal("false", cut.Find(CheckboxSelector).GetAttribute("aria-checked"));
        Assert.Contains(test.Logger.Entries, e => e.Message.StartsWith(
            "A component is changing the controlled checked state of Checkbox to be uncontrolled.", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "port: changing defaultChecked after the first render logs and changes nothing")]
    [Trait("Requirement", "CHK-EDGE-03")]
    public void Port_changing_defaultChecked_after_the_first_render_logs_and_changes_nothing()
    {
        IRenderedComponent<CheckboxRoot> cut = Context.Render<CheckboxRoot>(p => p.Add(x => x.DefaultChecked, false));

        cut.Render(p => p.Add(x => x.DefaultChecked, true));

        Assert.Equal("false", cut.Find(CheckboxSelector).GetAttribute("aria-checked"));
        (LogLevel level, string message) = Assert.Single(test.Logger.Entries);
        Assert.Equal(LogLevel.Error, level);
        Assert.Equal(
            "A component is changing the default checked state of an uncontrolled Checkbox after being initialized. To suppress this warning opt to use a controlled Checkbox.",
            message);
    }

    [Fact(DisplayName = "should update its state if the underlying input is toggled")]
    [Trait("Requirement", "CHK-PTR-04")]
    public async Task Should_update_its_state_if_the_underlying_input_is_toggled()
    {
        IRenderedComponent<CheckboxRoot> cut = Context.Render<CheckboxRoot>();

        await test.ToggleAsync(cut, true);

        Assert.Equal("true", cut.Find(CheckboxSelector).GetAttribute("aria-checked"));
    }

    [Fact(DisplayName = "uses aria-disabled instead of HTML disabled")]
    [Trait("Requirement", "CHK-ARIA-05")]
    [Trait("Requirement", "CHK-FOC-02")]
    [Trait("Requirement", "CHK-DOM-12")]
    public void Uses_aria_disabled_instead_of_HTML_disabled()
    {
        IRenderedComponent<CheckboxRoot> cut = Context.Render<CheckboxRoot>(p => p.Add(x => x.Disabled, true));
        IElement root = cut.Find(CheckboxSelector);

        Assert.False(root.HasAttribute("disabled"));
        Assert.Equal("true", root.GetAttribute("aria-disabled"));
        Assert.Equal("-1", root.GetAttribute("tabindex"));
        Assert.True(cut.Find("input[type=checkbox]").HasAttribute("disabled"));
    }

    [Fact(DisplayName = "port: a disabled box ignores a toggle that still arrives")]
    [Trait("Requirement", "CHK-EVT-07")]
    public async Task Port_a_disabled_box_ignores_a_toggle_that_still_arrives()
    {
        int calls = 0;
        IRenderedComponent<CheckboxRoot> cut = Context.Render<CheckboxRoot>(p => p.Add(x => x.Disabled, true).Add(x => x.OnCheckedChange, _ => calls++));

        Assert.Equal("stop", await test.DecideToggleAsync(cut));
        await test.ToggleAsync(cut, true);

        Assert.Equal(0, calls);
        Assert.Equal("false", cut.Find(CheckboxSelector).GetAttribute("aria-checked"));
        Assert.Contains("\"disabled\":true", test.LastButtonOptions, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "should have the `aria-readonly` attribute")]
    [Trait("Requirement", "CHK-ARIA-03")]
    [Trait("Requirement", "CHK-API-12")]
    public void Should_have_the_aria_readonly_attribute()
    {
        IRenderedComponent<CheckboxRoot> cut = Context.Render<CheckboxRoot>(p => p.Add(x => x.ReadOnly, true));

        Assert.Equal("true", cut.Find(CheckboxSelector).GetAttribute("aria-readonly"));
        Assert.Contains("\"readOnly\":true", test.LastButtonOptions, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "should not have the aria attribute when `readOnly` is not set")]
    [Trait("Requirement", "CHK-ARIA-03")]
    public void Should_not_have_the_aria_attribute_when_readOnly_is_not_set()
    {
        IRenderedComponent<CheckboxRoot> cut = Context.Render<CheckboxRoot>();

        Assert.False(cut.Find(CheckboxSelector).HasAttribute("aria-readonly"));
    }

    [Fact(DisplayName = "port: a read-only box ignores a toggle that still arrives, and stays focusable and submittable")]
    [Trait("Requirement", "CHK-API-12")]
    [Trait("Requirement", "CHK-EVT-07")]
    [Trait("Requirement", "CHK-EDGE-06")]
    public async Task Port_a_read_only_box_ignores_a_toggle()
    {
        int calls = 0;
        IRenderedComponent<CheckboxRoot> cut = Context.Render<CheckboxRoot>(p => p
            .Add(x => x.ReadOnly, true)
            .Add(x => x.Name, "n")
            .Add(x => x.OnCheckedChange, _ => calls++));

        Assert.Equal("stop", await test.DecideToggleAsync(cut));
        await test.ToggleAsync(cut, true);

        Assert.Equal(0, calls);
        IElement root = cut.Find(CheckboxSelector);
        Assert.Equal("false", root.GetAttribute("aria-checked"));
        Assert.Equal("0", root.GetAttribute("tabindex"));
        Assert.False(root.HasAttribute("data-disabled"));
        IElement input = cut.Find("input[type=checkbox]");
        Assert.Equal("n", input.GetAttribute("name"));
        Assert.False(input.HasAttribute("disabled"));
    }

    [Fact(DisplayName = "should set the `aria-checked` attribute as \"mixed\"")]
    [Trait("Requirement", "CHK-ARIA-02")]
    [Trait("Requirement", "CHK-API-04")]
    public void Should_set_the_aria_checked_attribute_as_mixed()
    {
        IRenderedComponent<CheckboxRoot> cut = Context.Render<CheckboxRoot>(p => p.Add(x => x.Indeterminate, true));

        Assert.Equal("mixed", cut.Find(CheckboxSelector).GetAttribute("aria-checked"));
    }

    [Fact(DisplayName = "should not have the aria attribute when `indeterminate` is not set")]
    [Trait("Requirement", "CHK-ARIA-02")]
    [Trait("Requirement", "CHK-API-04")]
    public void Should_not_have_the_aria_attribute_when_indeterminate_is_not_set()
    {
        IRenderedComponent<CheckboxRoot> cut = Context.Render<CheckboxRoot>();

        Assert.NotEqual("mixed", cut.Find(CheckboxSelector).GetAttribute("aria-checked"));
        Assert.False(cut.Find(CheckboxSelector).HasAttribute("data-indeterminate"));
    }

    [Fact(DisplayName = "should not change its state when clicked")]
    [Trait("Requirement", "CHK-STA-09")]
    public async Task Should_not_change_its_state_when_clicked_indeterminate()
    {
        List<bool> values = [];
        IRenderedComponent<CheckboxRoot> cut = Context.Render<CheckboxRoot>(p => p
            .Add(x => x.Indeterminate, true)
            .Add(x => x.OnCheckedChange, args => values.Add(args.Value)));
        Assert.Equal("mixed", cut.Find(CheckboxSelector).GetAttribute("aria-checked"));

        Assert.Equal("run", await test.DecideToggleAsync(cut));

        // The underlying value flips and the callback receives it; the rendering stays mixed.
        Assert.Equal([true], values);
        Assert.Equal("mixed", cut.Find(CheckboxSelector).GetAttribute("aria-checked"));
        Assert.Equal("""{"indeterminate":true,"checked":true,"liveRegion":false}""", test.DomOptions[^1]);
    }

    [Fact(DisplayName = "should not be overridden by `checked` prop")]
    [Trait("Requirement", "CHK-STA-09")]
    [Trait("Requirement", "CHK-API-04")]
    public void Should_not_be_overridden_by_checked_prop()
    {
        IRenderedComponent<CheckboxRoot> cut = Context.Render<CheckboxRoot>(p => p.Add(x => x.Indeterminate, true).Add(x => x.Checked, true));

        Assert.Equal("mixed", cut.Find(CheckboxSelector).GetAttribute("aria-checked"));
    }

    [Fact(DisplayName = "sets the native input state when indeterminate")]
    [Trait("Requirement", "CHK-API-04")]
    public void Sets_the_native_input_state_when_indeterminate()
    {
        // The property itself is the dom module's (tests/browser/checkbox.spec.ts); C# hands it the value on attach.
        Context.Render<CheckboxRoot>(p => p.Add(x => x.Indeterminate, true));

        Assert.Equal(["""{"indeterminate":true,"checked":false,"liveRegion":false}"""], test.DomOptions);
    }

    [Fact(DisplayName = "keeps the native input state when checked changes while indeterminate remains")]
    [Trait("Requirement", "CHK-STA-09")]
    [Trait("Requirement", "CHK-DOM-12")]
    public async Task Keeps_the_native_input_state_when_checked_changes_while_indeterminate_remains()
    {
        bool value = false;
        IRenderedComponent<CheckboxRoot> cut = null!;
        cut = Context.Render<CheckboxRoot>(p => p
            .Add(x => x.Indeterminate, true)
            .Add(x => x.Checked, value)
            .Add(x => x.OnCheckedChange, args => cut.Render(q => q.Add(x => x.Checked, value = args.Value))));

        await test.ToggleAsync(cut, true);

        Assert.True(value);
        Assert.Equal("mixed", cut.Find(CheckboxSelector).GetAttribute("aria-checked"));
        Assert.Equal("""{"indeterminate":true,"checked":true,"liveRegion":false}""", test.DomOptions[^1]);
    }

    [Fact(DisplayName = "sets indeterminate style hooks on the root and indicator")]
    [Trait("Requirement", "CHK-DOM-03")]
    [Trait("Requirement", "CHK-DOM-08")]
    [Trait("Requirement", "CHK-API-19")]
    public void Sets_indeterminate_style_hooks_on_the_root_and_indicator()
    {
        IRenderedComponent<CheckboxRoot> cut = Context.Render<CheckboxRoot>(p => p
            .Add(x => x.Indeterminate, true)
            .Add(x => x.DefaultChecked, true)
            .AddChildContent<CheckboxIndicator>(i => i.AddUnmatched("data-testid", "indicator")));

        foreach (IElement element in new[] { cut.Find(CheckboxSelector), cut.Find("[data-testid=indicator]") })
        {
            Assert.Equal(string.Empty, element.GetAttribute("data-indeterminate"));

            // CHK-DOM-03: neither checked attribute while mixed.
            Assert.False(element.HasAttribute("data-checked"));
            Assert.False(element.HasAttribute("data-unchecked"));
        }
    }

    [Fact(DisplayName = "should place the style hooks on the root and the indicator")]
    [Trait("Requirement", "CHK-DOM-02")]
    [Trait("Requirement", "CHK-DOM-04")]
    [Trait("Requirement", "CHK-DOM-08")]
    [Trait("Requirement", "CHK-STA-02")]
    [Trait("Requirement", "CHK-STA-01")]
    [Trait("Requirement", "CHK-STR-03")]
    [Trait("Requirement", "CHK-API-02")]
    public async Task Should_place_the_style_hooks_on_the_root_and_the_indicator()
    {
        CheckboxIndicatorState? indicatorState = null;
        IRenderedComponent<CheckboxRoot> cut = Context.Render<CheckboxRoot>(p => p
            .Add(x => x.DefaultChecked, true)
            .Add(x => x.Disabled, true)
            .Add(x => x.ReadOnly, true)
            .Add(x => x.Required, true)
            .AddChildContent<CheckboxIndicator>(i => i.Add(x => x.ClassValue, state =>
            {
                indicatorState = state;
                return null;
            })));

        IElement root = cut.Find(CheckboxSelector);
        IElement indicator = root.QuerySelector("span")!;
        foreach (IElement element in new[] { root, indicator })
        {
            Assert.Equal(string.Empty, element.GetAttribute("data-checked"));
            Assert.False(element.HasAttribute("data-unchecked"));
            Assert.Equal(string.Empty, element.GetAttribute("data-disabled"));
            Assert.Equal(string.Empty, element.GetAttribute("data-readonly"));
            Assert.Equal(string.Empty, element.GetAttribute("data-required"));
        }

        // CHK-STA-02: the root's ten fields and the transition status, which is unset for a box checked at mount.
        Assert.Equal(new CheckboxIndicatorState(true, true, true, true, false, false, false, null, false, false, null), indicatorState);

        // CHK-STR-03: the indicator carries no handler, ARIA or id of its own.
        Assert.Equal(
            ["data-checked", "data-disabled", "data-readonly", "data-required"],
            indicator.Attributes.Select(a => a.Name).Where(n => n != "blazor:elementreference").Order(StringComparer.Ordinal));

        cut.Render(p => p.Add(x => x.Disabled, false).Add(x => x.ReadOnly, false));
        await test.ToggleAsync(cut, false);

        root = cut.Find(CheckboxSelector);
        Assert.Equal(string.Empty, root.GetAttribute("data-unchecked"));
        Assert.False(root.HasAttribute("data-checked"));
    }

    [Fact(DisplayName = "should set the name attribute only on the input")]
    [Trait("Requirement", "CHK-API-08")]
    [Trait("Requirement", "CHK-DOM-17")]
    [Trait("Requirement", "CHK-DOM-12")]
    public void Should_set_the_name_attribute_only_on_the_input()
    {
        IRenderedComponent<CheckboxRoot> cut = Context.Render<CheckboxRoot>(p => p.Add(x => x.Name, "checkbox-name").Add(x => x.Value, "v"));

        Assert.Equal("checkbox-name", cut.Find("input[type=checkbox]").GetAttribute("name"));
        Assert.False(cut.Find(CheckboxSelector).HasAttribute("name"));
        Assert.False(cut.Find(CheckboxSelector).HasAttribute("value"));
    }

    [Fact(DisplayName = "port: the input's value is the prop when given, an empty string included, and absent otherwise")]
    [Trait("Requirement", "CHK-DOM-16")]
    [Trait("Requirement", "CHK-API-06")]
    public void Port_the_inputs_value_is_the_prop_when_given()
    {
        IRenderedComponent<CheckboxRoot> none = Context.Render<CheckboxRoot>(p => p.Add(x => x.Name, "n"));
        IRenderedComponent<CheckboxRoot> empty = Context.Render<CheckboxRoot>(p => p.Add(x => x.Value, string.Empty));
        IRenderedComponent<CheckboxRoot> given = Context.Render<CheckboxRoot>(p => p.Add(x => x.Value, "custom"));

        Assert.False(none.Find("input").HasAttribute("value"));
        Assert.Equal(string.Empty, empty.Find("input").GetAttribute("value"));
        Assert.Equal("custom", given.Find("input").GetAttribute("value"));
    }

    [Fact(DisplayName = "port: form is placed on both hidden inputs and never on the root")]
    [Trait("Requirement", "CHK-API-09")]
    [Trait("Requirement", "CHK-DOM-12")]
    public void Port_form_is_placed_on_both_hidden_inputs_and_never_on_the_root()
    {
        IRenderedComponent<CheckboxRoot> cut = Context.Render<CheckboxRoot>(p => p
            .Add(x => x.Form, "external")
            .Add(x => x.Name, "n")
            .Add(x => x.UncheckedValue, "off"));

        Assert.Equal("external", cut.Find("input[type=checkbox]").GetAttribute("form"));
        Assert.Equal("external", cut.Find("input[type=hidden]").GetAttribute("form"));
        Assert.False(cut.Find(CheckboxSelector).HasAttribute("form"));
    }

    [Fact(DisplayName = "port: the unchecked-value input sits between the root and the checkbox only while unchecked, named, not a parent and given a value")]
    [Trait("Requirement", "CHK-DOM-19")]
    [Trait("Requirement", "CHK-API-07")]
    [Trait("Requirement", "CHK-STR-01")]
    [Trait("Requirement", "CHK-ANI-09")]
    public async Task Port_the_unchecked_value_input_sits_between_the_root_and_the_checkbox()
    {
        IRenderedComponent<CheckboxRoot> cut = Context.Render<CheckboxRoot>(p => p
            .Add(x => x.Name, "n")
            .Add(x => x.UncheckedValue, "off")
            .Add(x => x.Disabled, true)
            .Add(x => x.Form, "f"));

        Assert.Equal(["SPAN", "INPUT", "INPUT"], cut.Nodes.OfType<IElement>().Select(e => e.TagName));
        IElement hidden = cut.Find("input[type=hidden]");
        Assert.Equal(("n", "off", "f", true), (hidden.GetAttribute("name"), hidden.GetAttribute("value"), hidden.GetAttribute("form"), hidden.HasAttribute("disabled")));

        cut.Render(p => p.Add(x => x.Disabled, false));
        await test.ToggleAsync(cut, true);
        Assert.Empty(cut.FindAll("input[type=hidden]"));

        await test.ToggleAsync(cut, false);
        Assert.Single(cut.FindAll("input[type=hidden]"));

        cut.Render(p => p.Add(x => x.Name, null));
        Assert.Empty(cut.FindAll("input[type=hidden]"));

        IRenderedComponent<CheckboxRoot> noValue = Context.Render<CheckboxRoot>(p => p.Add(x => x.Name, "n"));
        Assert.Empty(noValue.FindAll("input[type=hidden]"));

        IRenderedComponent<CheckboxRoot> parent = Context.Render<CheckboxRoot>(p => p.Add(x => x.Name, "n").Add(x => x.UncheckedValue, "off").Add(x => x.Parent, true));
        Assert.Empty(parent.FindAll("input[type=hidden]"));
    }

    [Fact(DisplayName = "port: a parent box outside a group keeps its own state, carries data-parent and submits nothing")]
    [Trait("Requirement", "CHK-EDGE-11")]
    [Trait("Requirement", "CHK-DOM-07")]
    [Trait("Requirement", "CHK-API-05")]
    [Trait("Requirement", "CHK-DOM-12")]
    public async Task Port_a_parent_box_outside_a_group_keeps_its_own_state()
    {
        IRenderedComponent<CheckboxRoot> cut = Context.Render<CheckboxRoot>(p => p
            .Add(x => x.Parent, true)
            .Add(x => x.Name, "n")
            .AddChildContent<CheckboxIndicator>(i => i.Add(x => x.KeepMounted, true).AddUnmatched("data-testid", "indicator")));

        IElement root = cut.Find(CheckboxSelector);
        Assert.Equal(string.Empty, root.GetAttribute("data-parent"));
        Assert.False(cut.Find("[data-testid=indicator]").HasAttribute("data-parent"));
        Assert.False(cut.Find("input[type=checkbox]").HasAttribute("name"));

        await test.ToggleAsync(cut, true);
        Assert.Equal("true", cut.Find(CheckboxSelector).GetAttribute("aria-checked"));

        IRenderedComponent<CheckboxRoot> plain = Context.Render<CheckboxRoot>();
        Assert.False(plain.Find(CheckboxSelector).HasAttribute("data-parent"));
    }

    [Fact(DisplayName = "port: the hidden input is a checkbox hidden from the accessibility tree and the tab order")]
    [Trait("Requirement", "CHK-DOM-11")]
    public void Port_the_hidden_input_is_a_checkbox_hidden_from_the_accessibility_tree_and_the_tab_order()
    {
        IRenderedComponent<CheckboxRoot> cut = Context.Render<CheckboxRoot>();
        IElement input = cut.Find("input");

        Assert.Equal(("checkbox", "true", "-1"), (input.GetAttribute("type"), input.GetAttribute("aria-hidden"), input.GetAttribute("tabindex")));
    }

    [Fact(DisplayName = "port: the hidden input is visually hidden, kept in flow beside the box when named")]
    [Trait("Requirement", "CHK-DOM-14")]
    public void Port_the_hidden_input_is_visually_hidden()
    {
        IRenderedComponent<CheckboxRoot> nameless = Context.Render<CheckboxRoot>();
        IRenderedComponent<CheckboxRoot> named = Context.Render<CheckboxRoot>(p => p.Add(x => x.Name, "n"));

        Assert.Equal(
            "clip-path: inset(50%); overflow: hidden; white-space: nowrap; border: 0; padding: 0; width: 1px; height: 1px; margin: 0; position: fixed; top: 0; left: 0",
            nameless.Find("input").GetAttribute("style"));
        Assert.Equal(
            "clip-path: inset(50%); overflow: hidden; white-space: nowrap; border: 0; padding: 0; width: 1px; height: 1px; margin: -1px; position: absolute",
            named.Find("input").GetAttribute("style"));
    }

    [Fact(DisplayName = "port: the hidden input renders the initial checked value once and leaves it to the dom module after")]
    [Trait("Requirement", "CHK-DOM-12")]
    [Trait("Requirement", "CHK-EDGE-10")]
    public async Task Port_the_hidden_input_renders_the_initial_checked_value_once()
    {
        IRenderedComponent<CheckboxRoot> cut = Context.Render<CheckboxRoot>(p => p.Add(x => x.DefaultChecked, true));
        Assert.True(cut.Find("input").HasAttribute("checked"));

        await test.ToggleAsync(cut, false);

        Assert.True(cut.Find("input").HasAttribute("checked"));
        Assert.Equal(
            ["""{"indeterminate":false,"checked":true,"liveRegion":false}""", """{"indeterminate":false,"checked":false,"liveRegion":false}"""],
            test.DomOptions);
    }

    [Fact(DisplayName = "should toggle the checkbox when a explicitly linked <label> is clicked")]
    [Trait("Requirement", "CHK-DOM-15")]
    [Trait("Requirement", "CHK-API-14")]
    [Trait("Requirement", "CHK-DOM-01")]
    public async Task Should_toggle_the_checkbox_when_a_explicitly_linked_label_is_clicked()
    {
        IRenderedComponent<CheckboxRoot> cut = Context.Render<CheckboxRoot>(p => p.AddUnmatched("id", "myCheckbox"));

        // The id is the input's (the label targets it); the root keeps a generated id.
        Assert.Equal("myCheckbox", cut.Find("input").GetAttribute("id"));
        Assert.StartsWith(PartIdGenerator.Prefix, cut.Find(CheckboxSelector).GetAttribute("id"), StringComparison.Ordinal);

        await test.ToggleAsync(cut, true);
        Assert.Equal("true", cut.Find(CheckboxSelector).GetAttribute("aria-checked"));
    }

    [Fact(DisplayName = "should associate `id` with the native button when `nativeButton=true`")]
    [Trait("Requirement", "CHK-DOM-01")]
    [Trait("Requirement", "CHK-DOM-15")]
    [Trait("Requirement", "CHK-ARIA-09")]
    [Trait("Requirement", "CHK-API-14")]
    public void Should_associate_id_with_the_native_button_when_nativeButton_true()
    {
        IRenderedComponent<CheckboxRoot> cut = Context.Render<CheckboxRoot>(p => p
            .AddUnmatched("id", "myCheckbox")
            .Add(x => x.NativeButton, true)
            .Add(x => x.Render, s_nativeButtonElement));

        IElement root = cut.Find(CheckboxSelector);
        Assert.Equal("myCheckbox", root.GetAttribute("id"));
        Assert.False(cut.Find("input").HasAttribute("id"));
        Assert.False(root.HasAttribute("aria-labelledby"));
    }

    [Fact(DisplayName = "port: an empty id counts as absent, and without one each element gets its own generated id")]
    [Trait("Requirement", "CHK-API-14")]
    [Trait("Requirement", "CHK-DOM-01")]
    [Trait("Requirement", "CHK-DOM-15")]
    public void Port_an_empty_id_counts_as_absent()
    {
        IRenderedComponent<CheckboxRoot> empty = Context.Render<CheckboxRoot>(p => p.AddUnmatched("id", string.Empty));
        IRenderedComponent<CheckboxRoot> native = Context.Render<CheckboxRoot>(p => p.AddUnmatched("id", string.Empty).Add(x => x.NativeButton, true));

        string rootId = empty.Find(CheckboxSelector).GetAttribute("id")!;
        string inputId = empty.Find("input").GetAttribute("id")!;
        Assert.StartsWith(PartIdGenerator.Prefix, rootId, StringComparison.Ordinal);
        Assert.StartsWith(PartIdGenerator.Prefix, inputId, StringComparison.Ordinal);
        Assert.NotEqual(rootId, inputId);
        Assert.StartsWith(PartIdGenerator.Prefix, native.Find(CheckboxSelector).GetAttribute("id"), StringComparison.Ordinal);
        Assert.False(native.Find("input").HasAttribute("id"));
    }

    [Fact(DisplayName = "updates fallback `aria-labelledby` when the hidden input id changes")]
    [Trait("Requirement", "CHK-DOM-22")]
    public void Updates_fallback_aria_labelledby_when_the_hidden_input_id_changes()
    {
        // The label half (the fallback through the form module) is tests/browser/checkbox.spec.ts.
        IRenderedComponent<CheckboxRoot> cut = Context.Render<CheckboxRoot>(p => p.AddUnmatched("id", "checkbox-input-a"));
        string rootId = cut.Find(CheckboxSelector).GetAttribute("id")!;

        cut.Render(p => p.AddUnmatched("id", "checkbox-input-b"));

        Assert.Equal("checkbox-input-b", cut.Find("input").GetAttribute("id"));
        Assert.Equal(rootId, cut.Find(CheckboxSelector).GetAttribute("id"));
    }

    [Fact(DisplayName = "can render a native button")]
    [Trait("Requirement", "CHK-API-15")]
    [Trait("Requirement", "CHK-FOC-01")]
    [Trait("Requirement", "CHK-DOM-10")]
    public void Can_render_a_native_button()
    {
        IRenderedComponent<CheckboxRoot> cut = Context.Render<CheckboxRoot>(p => p
            .Add(x => x.NativeButton, true)
            .Add(x => x.Render, s_nativeButtonElement));

        IElement root = cut.Find(CheckboxSelector);
        Assert.Equal("BUTTON", root.TagName);
        Assert.Equal("false", root.GetAttribute("aria-checked"));
        Assert.Equal("button", root.GetAttribute("type"));
        Assert.Equal("0", root.GetAttribute("tabindex"));
        Assert.Contains("\"native\":true", test.LastButtonOptions, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "can be activated with Space key")]
    [Trait("Requirement", "CHK-FOC-01")]
    [Trait("Requirement", "CHK-DOM-10")]
    public void Can_be_activated_with_Space_key()
    {
        // The keys are the button module's (tests/browser/checkbox.spec.ts); the root is the only tab stop.
        IRenderedComponent<CheckboxRoot> cut = Context.Render<CheckboxRoot>();
        IElement root = cut.Find(CheckboxSelector);

        Assert.Equal("SPAN", root.TagName);
        Assert.Equal("0", root.GetAttribute("tabindex"));
        Assert.False(root.HasAttribute("type"));
        Assert.Equal("-1", cut.Find("input").GetAttribute("tabindex"));
    }

    [Fact(DisplayName = "port: the root's button behavior suppresses Enter and bridges clicks to the hidden input")]
    [Trait("Requirement", "CHK-KEY-03")]
    [Trait("Requirement", "CHK-PTR-01")]
    public void Port_the_roots_button_behavior_suppresses_Enter_and_bridges_clicks()
    {
        IRenderedComponent<CheckboxRoot> cut = Context.Render<CheckboxRoot>();

        Assert.Equal(
            """{"disabled":false,"native":false,"composite":false,"hiddenInput":true,"suppressEnter":true,"reportInteractionType":false,"developmentChecks":true}""",
            Assert.Single(test.ButtonModule.Invocations["attach"]).Arguments[1]);
        Assert.Equal(cut.Instance.Element, Assert.Single(test.ButtonModule.Invocations["attach"]).Arguments[0]);
    }

    [Fact(DisplayName = "port: the root's form handle submits the owning form on Enter, except while disabled (pending the user)")]
    [Trait("Requirement", "CHK-KEY-04")]
    [Trait("Requirement", "CHK-KEY-06")]
    public void Port_the_roots_form_handle_submits_the_owning_form_on_Enter()
    {
        // Design point C2 of M2 track 5: the form module owns the Enter path (ADR 04 §1's form row); outside a field the
        // root's handle reports nothing.
        IRenderedComponent<CheckboxRoot> cut = Context.Render<CheckboxRoot>();
        Blazix.Primitives.Interop.FormControlOptions root = test.FormOptions.First(o => o.EnterSubmit);
        Assert.True(root.EnterSubmit);
        Assert.False(root.ReportEvents);
        Assert.False(root.Disabled);
        Assert.Equal(cut.Instance.Element, test.FormModule.Invocations["attach"][0].Arguments[0]);

        cut.Render(p => p.Add(x => x.Disabled, true));
        Assert.True(test.FormOptions.Last(o => o.EnterSubmit).Disabled);
    }

    [Fact(DisplayName = "port: the hidden input looks for a native label while nothing else names the box, and hands focus to the root (pending the user)")]
    [Trait("Requirement", "CHK-ARIA-07")]
    [Trait("Requirement", "CHK-ARIA-08")]
    [Trait("Requirement", "CHK-ARIA-09")]
    [Trait("Requirement", "CHK-FOC-03")]
    public async Task Port_the_hidden_input_looks_for_a_native_label_while_nothing_else_names_the_box()
    {
        // Design point C4 of M2 track 5: the form module discovers the label (ADR 04 §1's form row); C# renders the id.
        IRenderedComponent<CheckboxRoot> cut = Context.Render<CheckboxRoot>(p => p.AddUnmatched("id", "box"));
        string rootId = cut.Find(CheckboxSelector).GetAttribute("id")!;
        Blazix.Primitives.Interop.FormControlOptions input = test.FormOptions.Last(o => !o.ReportEvents && !o.EnterSubmit);
        Assert.Equal((true, rootId), (input.LabelFallback, input.FocusTarget));

        await test.ReportLabelAsync(cut, "found");
        Assert.Equal("found", cut.Find(CheckboxSelector).GetAttribute("aria-labelledby"));

        // CHK-ARIA-08: the module watches the document's labels and ids itself, so an unchanged render sends nothing.
        int updates = test.FormHandle.Invocations["update"].Count;
        cut.Render();
        Assert.Equal(updates, test.FormHandle.Invocations["update"].Count);

        // A non-blank aria-label names the box instead (CHK-ARIA-06 step 2); a native button has no fallback (CHK-ARIA-09).
        cut.Render(p => p.AddUnmatched("id", "box").AddUnmatched("aria-label", "Name"));
        Assert.False(test.FormOptions.Last(o => !o.ReportEvents && !o.EnterSubmit).LabelFallback);
        Assert.False(cut.Find(CheckboxSelector).HasAttribute("aria-labelledby"));

        Context.Render<CheckboxRoot>(p => p.Add(x => x.NativeButton, true));
        Assert.False(test.FormOptions.Last(o => !o.ReportEvents && !o.EnterSubmit).LabelFallback);
    }

    [Fact(DisplayName = "should call onClick when clicked")]
    [Trait("Requirement", "CHK-PTR-09")]
    [Trait("Requirement", "CHK-API-17")]
    public async Task Should_call_onClick_when_clicked()
    {
        int clicks = 0;
        IRenderedComponent<CheckboxRoot> cut = Context.Render<CheckboxRoot>(p => p.AddUnmatched("onclick", EventCallback.Factory.Create<MouseEventArgs>(this, () => clicks++)));

        // Server: Blazor dispatches the root click to the part's handler, which runs the consumer's.
        await cut.Find(CheckboxSelector).ClickAsync(new MouseEventArgs());
        Assert.Equal(1, clicks);

        // WebAssembly: the activation decision runs it inside the dispatch, and Blazor's dispatch of the same click skips it.
        Assert.Equal("run", await test.DecideRootClickAsync(cut));
        await cut.Find(CheckboxSelector).ClickAsync(new MouseEventArgs { Detail = 1 });
        Assert.Equal(2, clicks);
    }

    [Fact(DisplayName = "port: a consumer onClick that prevents the part's handler stops the toggle on WebAssembly")]
    [Trait("Requirement", "CHK-PTR-09")]
    public async Task Port_a_consumer_onClick_that_prevents_the_parts_handler_stops_the_toggle()
    {
        IRenderedComponent<CheckboxRoot> cut = Context.Render<CheckboxRoot>(p => p
            .AddUnmatched("onclick", EventCallback.Factory.Create<MouseEventArgs>(this, e => e.PreventPartHandler())));

        Assert.Equal("stop", await test.DecideRootClickAsync(cut));
    }

    [Fact(DisplayName = "port: the twins attach once to the root and to the hidden input, and an unchanged render sends nothing")]
    [Trait("Requirement", "CHK-DOM-13")]
    [Trait("Requirement", "CHK-ANI-09")]
    public void Port_the_twins_attach_once()
    {
        IRenderedComponent<CheckboxRoot> cut = Context.Render<CheckboxRoot>();

        cut.Render();
        cut.Render();

        Assert.Single(test.ButtonModule.Invocations["attach"]);
        Assert.Single(test.DomModule.Invocations["attach"]);
        Assert.Empty(test.DomHandle.Invocations["update"]);
        Assert.NotEqual(cut.Instance.Element, test.DomModule.Invocations["attach"][0].Arguments[0]);
    }

    [Fact(DisplayName = "port: changing indeterminate updates the dom module and the rendering, never the value")]
    [Trait("Requirement", "CHK-API-04")]
    [Trait("Requirement", "CHK-STA-09")]
    public void Port_changing_indeterminate_updates_the_dom_module()
    {
        IRenderedComponent<CheckboxRoot> cut = Context.Render<CheckboxRoot>(p => p.Add(x => x.DefaultChecked, true));

        cut.Render(p => p.Add(x => x.Indeterminate, true));
        Assert.Equal("mixed", cut.Find(CheckboxSelector).GetAttribute("aria-checked"));

        cut.Render(p => p.Add(x => x.Indeterminate, false));
        Assert.Equal("true", cut.Find(CheckboxSelector).GetAttribute("aria-checked"));
        Assert.Equal(
            [
                """{"indeterminate":false,"checked":true,"liveRegion":false}""",
                """{"indeterminate":true,"checked":true,"liveRegion":false}""",
                """{"indeterminate":false,"checked":true,"liveRegion":false}""",
            ],
            test.DomOptions);
    }

    [Fact(DisplayName = "port: disposing the box disposes both handles")]
    public async Task Port_disposing_the_box_disposes_both_handles()
    {
        Context.Render<CheckboxRoot>();

        await Context.DisposeComponentsAsync();

        Assert.Single(test.ButtonHandle.Invocations["dispose"]);
        Assert.Single(test.DomHandle.Invocations["dispose"]);
    }

    [Fact(DisplayName = "port: the root's data attribute names are Base UI's twelve")]
    [Trait("Requirement", "CHK-DOM-02")]
    public void Port_the_roots_data_attribute_names_are_Base_UIs_twelve()
    {
        Assert.Equal(
            ["data-checked", "data-unchecked", "data-indeterminate", "data-disabled", "data-readonly", "data-required", "data-valid", "data-invalid", "data-touched", "data-dirty", "data-filled", "data-focused"],
            [
                CheckboxRootDataAttributes.Checked, CheckboxRootDataAttributes.Unchecked, CheckboxRootDataAttributes.Indeterminate,
                CheckboxRootDataAttributes.Disabled, CheckboxRootDataAttributes.Readonly, CheckboxRootDataAttributes.Required,
                CheckboxRootDataAttributes.Valid, CheckboxRootDataAttributes.Invalid, CheckboxRootDataAttributes.Touched,
                CheckboxRootDataAttributes.Dirty, CheckboxRootDataAttributes.Filled, CheckboxRootDataAttributes.Focused,
            ]);
        Assert.Equal("data-starting-style", CheckboxIndicatorDataAttributes.StartingStyle);
        Assert.Equal("data-ending-style", CheckboxIndicatorDataAttributes.EndingStyle);
        Assert.Equal("data-indeterminate", CheckboxIndicatorDataAttributes.Indeterminate);
    }
}
