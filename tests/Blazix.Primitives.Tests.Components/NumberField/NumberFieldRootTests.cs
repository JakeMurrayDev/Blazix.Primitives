using System.Globalization;
using AngleSharp.Dom;
using Blazix.Primitives.Utilities;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// NumberFieldRoot.test.tsx, the rows that need no typed text and no field (issue 277, 6a). Base UI's `NumberField` wrapper
// renders a Group with the Input, both steppers and a ScrubArea; the ScrubArea is 6b's, so the wrapper here has the rest. A
// stepper click is the button twin's single-action click report (NumberFieldTestContext). Base UI's tests format with the
// runtime's default locale, en-US in the browser runs; these pass en-US as Culture, as CI runs the invariant culture, and
// compare with the port's formatter where Base UI compares with `new Intl.NumberFormat(...)`.
/// <summary>The number field root: its value, its text, its hidden input and its parameters.</summary>
public sealed class NumberFieldRootTests : IDisposable
{
    private static readonly CultureInfo s_enUs = CultureInfo.GetCultureInfo("en-US");

    private readonly NumberFieldTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "should accept a number value")]
    [Trait("Requirement", "NUM-API-02")]
    [Trait("Requirement", "NUM-STA-05")]
    public void Should_accept_a_number_value()
    {
        IRenderedComponent<NumberFieldRoot> cut = Render(p => p.Add(x => x.DefaultValue, 1));

        Assert.Equal("1", test.Text);
    }

    [Fact(DisplayName = "should accept an `undefined` value")]
    [Trait("Requirement", "NUM-API-02")]
    public void Should_accept_an_undefined_value()
    {
        IRenderedComponent<NumberFieldRoot> cut = Render(p => p.Add(x => x.DefaultValue, 3));
        Assert.Equal("3", test.Text);

        IRenderedComponent<NumberFieldRoot> empty = Render();
        Assert.Equal(string.Empty, Input(empty).GetAttribute("value"));
    }

    [Fact(DisplayName = "should accept a number value that can change over time")]
    [Trait("Requirement", "NUM-API-01")]
    [Trait("Requirement", "NUM-STA-04")]
    public void Should_accept_a_number_value_that_can_change_over_time()
    {
        IRenderedComponent<NumberFieldRoot> cut = Render(p => p.Add(x => x.Value, 1));
        Assert.Equal("1", test.Text);

        cut.Render(p => p.Add(x => x.Value, 2));

        Assert.Equal("2", test.Text);
    }

    [Fact(DisplayName = "should accept a `null` value")]
    [Trait("Requirement", "NUM-API-01")]
    public async Task Should_accept_a_null_value()
    {
        List<double?> changes = [];
        IRenderedComponent<NumberFieldRoot> cut = Render(p => p
            .Add(x => x.Value, null)
            .Add(x => x.OnValueChange, (ValueChangeEventArgs<double?> e) => changes.Add(e.Value)));
        Assert.Equal(string.Empty, test.Text);

        // Controlled by null: a step reports its value and the field keeps showing the consumer's null.
        await test.ClickAsync(cut, cut.FindComponent<NumberFieldIncrement>().Instance);

        Assert.Equal([0d], changes);
        Assert.Equal(string.Empty, test.Text);
    }

    [Fact(DisplayName = "includes the reason for increment button presses")]
    [Trait("Requirement", "NUM-EVT-02")]
    [Trait("Requirement", "NUM-EVT-03")]
    public async Task Includes_the_reason_for_increment_button_presses()
    {
        List<ValueChangeEventArgs<double?>> changes = [];
        IRenderedComponent<NumberFieldRoot> cut = Render(p => p.Add(x => x.DefaultValue, 1).Add(x => x.OnValueChange, (ValueChangeEventArgs<double?> e) => changes.Add(e)));

        await test.ClickAsync(cut, cut.FindComponent<NumberFieldIncrement>().Instance);

        ValueChangeEventArgs<double?> change = Assert.Single(changes);
        Assert.Equal(EventReason.IncrementPress, change.Details.Reason);
        Assert.Equal(1, Assert.IsType<NumberFieldChangeEventDetails>(change.Details).Direction);
    }

    [Fact(DisplayName = "includes the reason for decrement button presses")]
    [Trait("Requirement", "NUM-EVT-02")]
    [Trait("Requirement", "NUM-EVT-03")]
    public async Task Includes_the_reason_for_decrement_button_presses()
    {
        List<ValueChangeEventArgs<double?>> changes = [];
        IRenderedComponent<NumberFieldRoot> cut = Render(p => p.Add(x => x.DefaultValue, 1).Add(x => x.OnValueChange, (ValueChangeEventArgs<double?> e) => changes.Add(e)));

        await test.ClickAsync(cut, cut.FindComponent<NumberFieldDecrement>().Instance);

        ValueChangeEventArgs<double?> change = Assert.Single(changes);
        Assert.Equal(EventReason.DecrementPress, change.Details.Reason);
        Assert.Equal(-1, Assert.IsType<NumberFieldChangeEventDetails>(change.Details).Direction);
    }

    [Fact(DisplayName = "should disable the input")]
    [Trait("Requirement", "NUM-API-13")]
    [Trait("Requirement", "NUM-DOM-08")]
    [Trait("Requirement", "NUM-EDGE-12")]
    public void Should_disable_the_input()
    {
        IRenderedComponent<NumberFieldRoot> cut = Render(p => p.Add(x => x.Disabled, true));

        Assert.True(Input(cut).HasAttribute("disabled"));
        Assert.True(HiddenInput(cut).HasAttribute("disabled"));
    }

    [Fact(DisplayName = "should mark the input as readOnly")]
    [Trait("Requirement", "NUM-API-14")]
    [Trait("Requirement", "NUM-DOM-08")]
    public void Should_mark_the_input_as_readOnly()
    {
        IRenderedComponent<NumberFieldRoot> cut = Render(p => p.Add(x => x.ReadOnly, true));

        Assert.True(Input(cut).HasAttribute("readonly"));
    }

    [Fact(DisplayName = "does not render aria-readonly on stepper buttons")]
    [Trait("Requirement", "NUM-ARIA-07")]
    [Trait("Requirement", "NUM-ARIA-08")]
    [Trait("Requirement", "NUM-API-23")]
    [Trait("Requirement", "NUM-STA-20")]
    public void Does_not_render_aria_readonly_on_stepper_buttons()
    {
        IRenderedComponent<NumberFieldRoot> cut = Render(p => p.Add(x => x.ReadOnly, true));

        Assert.True(Input(cut).HasAttribute("readonly"));
        foreach (string label in new[] { "Increase", "Decrease" })
        {
            IElement button = cut.Find($"button[aria-label={label}]");
            Assert.False(button.HasAttribute("aria-readonly"));
            Assert.False(button.HasAttribute("aria-hidden"));
            Assert.False(button.HasAttribute("disabled"));
            Assert.Equal("true", button.GetAttribute("aria-disabled"));
        }
    }

    [Fact(DisplayName = "should mark the input as required")]
    [Trait("Requirement", "NUM-API-15")]
    [Trait("Requirement", "NUM-DOM-08")]
    public void Should_mark_the_input_as_required()
    {
        IRenderedComponent<NumberFieldRoot> cut = Render(p => p.Add(x => x.Required, true));

        Assert.True(Input(cut).HasAttribute("required"));
        Assert.True(HiddenInput(cut).HasAttribute("required"));
    }

    [Fact(DisplayName = "should set the name attribute on the hidden input")]
    [Trait("Requirement", "NUM-STR-01")]
    [Trait("Requirement", "NUM-API-16")]
    [Trait("Requirement", "NUM-DOM-06")]
    [Trait("Requirement", "NUM-DOM-18")]
    public void Should_set_the_name_attribute_on_the_hidden_input()
    {
        IRenderedComponent<NumberFieldRoot> cut = Render(p => p.Add(x => x.Name, "test"));

        // NUM-STR-01: the root, then the hidden input, as siblings; the hidden input is never inside the root.
        Assert.Equal(["DIV", "INPUT"], cut.Nodes.OfType<IElement>().Select(e => e.TagName));
        Assert.Equal("test", HiddenInput(cut).GetAttribute("name"));
        Assert.False(Input(cut).HasAttribute("name"));
        Assert.Equal(VisuallyHidden.InputStyle, HiddenInput(cut).GetAttribute("style"));
    }

    [Fact(DisplayName = "marks the hidden input readOnly when the field is readOnly")]
    [Trait("Requirement", "NUM-API-14")]
    [Trait("Requirement", "NUM-DOM-06")]
    public void Marks_the_hidden_input_readOnly_when_the_field_is_readOnly()
    {
        IRenderedComponent<NumberFieldRoot> cut = Render(p => p.Add(x => x.Name, "test").Add(x => x.ReadOnly, true));

        Assert.True(HiddenInput(cut).HasAttribute("readonly"));
    }

    // NUM-DOM-06/07, NUM-ARIA-10, NUM-API-06, NUM-API-17: the hidden input's attributes in one port title.
    [Fact(DisplayName = "port: the hidden input carries the raw value, bounds, step and form, and no id")]
    [Trait("Requirement", "NUM-DOM-06")]
    [Trait("Requirement", "NUM-DOM-07")]
    [Trait("Requirement", "NUM-ARIA-10")]
    [Trait("Requirement", "NUM-API-06")]
    [Trait("Requirement", "NUM-API-17")]
    public void The_hidden_input_carries_the_raw_value_bounds_step_and_form_and_no_id()
    {
        IRenderedComponent<NumberFieldRoot> cut = Render(p => p
            .Add(x => x.DefaultValue, 1234.5)
            .Add(x => x.Min, 0.5)
            .Add(x => x.Max, 1e21)
            .Add(x => x.Step, StepValue.Any)
            .Add(x => x.Form, "external"));
        IElement hidden = HiddenInput(cut);

        Assert.Equal("1,234.5", test.Text);
        Assert.Equal("1234.5", hidden.GetAttribute("value"));
        Assert.Equal("0.5", hidden.GetAttribute("min"));
        Assert.Equal("1e+21", hidden.GetAttribute("max"));
        Assert.Equal("any", hidden.GetAttribute("step"));
        Assert.Equal("external", hidden.GetAttribute("form"));
        Assert.Equal("true", hidden.GetAttribute("aria-hidden"));
        Assert.Equal("-1", hidden.GetAttribute("tabindex"));
        Assert.Equal(VisuallyHidden.Style, hidden.GetAttribute("style"));
        Assert.False(hidden.HasAttribute("id"));
        Assert.False(cut.Find("div").HasAttribute("form"));

        IRenderedComponent<NumberFieldRoot> defaults = Render();
        Assert.Equal("1", HiddenInput(defaults).GetAttribute("step"));
        Assert.Equal(string.Empty, HiddenInput(defaults).GetAttribute("value"));
        Assert.False(HiddenInput(defaults).HasAttribute("min"));
    }

    [Fact(DisplayName = "defaults to 1")]
    [Trait("Requirement", "NUM-API-06")]
    public async Task Defaults_to_1()
    {
        IRenderedComponent<NumberFieldRoot> cut = Render(p => p.Add(x => x.DefaultValue, 5));

        await test.ClickAsync(cut, cut.FindComponent<NumberFieldIncrement>().Instance);

        Assert.Equal("6", test.Text);
    }

    [Fact(DisplayName = "should increment the value by the `step` prop")]
    [Trait("Requirement", "NUM-API-06")]
    public async Task Should_increment_the_value_by_the_step_prop()
    {
        IRenderedComponent<NumberFieldRoot> cut = Render(p => p.Add(x => x.DefaultValue, 4).Add(x => x.Step, (StepValue)2));

        await test.ClickAsync(cut, cut.FindComponent<NumberFieldIncrement>().Instance);

        Assert.Equal("6", test.Text);
    }

    [Fact(DisplayName = "should snap when incrementing to the nearest multiple of the `step` prop")]
    [Trait("Requirement", "NUM-API-08")]
    public async Task Should_snap_when_incrementing_to_the_nearest_multiple_of_the_step_prop()
    {
        IRenderedComponent<NumberFieldRoot> cut = Render(p => p.Add(x => x.DefaultValue, 5).Add(x => x.Step, (StepValue)2).Add(x => x.SnapOnStep, true));

        await test.ClickAsync(cut, cut.FindComponent<NumberFieldIncrement>().Instance);

        Assert.Equal("6", test.Text);
    }

    [Fact(DisplayName = "should decrement the value by the `step` prop")]
    [Trait("Requirement", "NUM-API-06")]
    public async Task Should_decrement_the_value_by_the_step_prop()
    {
        IRenderedComponent<NumberFieldRoot> cut = Render(p => p.Add(x => x.DefaultValue, 6).Add(x => x.Step, (StepValue)2));

        await test.ClickAsync(cut, cut.FindComponent<NumberFieldDecrement>().Instance);

        Assert.Equal("4", test.Text);
    }

    [Fact(DisplayName = "should snap when decrementing to the nearest multiple of the `step` prop")]
    [Trait("Requirement", "NUM-API-08")]
    public async Task Should_snap_when_decrementing_to_the_nearest_multiple_of_the_step_prop()
    {
        IRenderedComponent<NumberFieldRoot> cut = Render(p => p.Add(x => x.DefaultValue, 5).Add(x => x.Step, (StepValue)2).Add(x => x.SnapOnStep, true));

        await test.ClickAsync(cut, cut.FindComponent<NumberFieldDecrement>().Instance);

        Assert.Equal("4", test.Text);
    }

    // Base UI's describe.skipIf(isJSDOM) block (the modifier keys of a jsdom click are dropped); the click report carries
    // them, so the titles run at L2.
    [Fact(DisplayName = "should increment the value by the default `largeStep` prop of 10 while holding the shift key")]
    [Trait("Requirement", "NUM-API-07")]
    [Trait("Requirement", "NUM-STA-16")]
    public async Task Should_increment_by_the_default_largeStep_while_holding_shift()
    {
        IRenderedComponent<NumberFieldRoot> cut = Render(p => p.Add(x => x.DefaultValue, 5));

        await test.ClickAsync(cut, cut.FindComponent<NumberFieldIncrement>().Instance, shiftKey: true);

        Assert.Equal("15", test.Text);
    }

    [Fact(DisplayName = "should increment the value by the default `smallStep` prop of 0.1 while holding the alt key")]
    [Trait("Requirement", "NUM-API-07")]
    [Trait("Requirement", "NUM-STA-16")]
    public async Task Should_increment_by_the_default_smallStep_while_holding_alt()
    {
        IRenderedComponent<NumberFieldRoot> cut = Render(p => p.Add(x => x.DefaultValue, 5));

        await test.ClickAsync(cut, cut.FindComponent<NumberFieldIncrement>().Instance, altKey: true);

        Assert.Equal("5.1", test.Text);
    }

    [Fact(DisplayName = "should use explicit `largeStep` value if provided while holding the shift key")]
    [Trait("Requirement", "NUM-STA-16")]
    public async Task Should_use_explicit_largeStep_while_holding_shift()
    {
        IRenderedComponent<NumberFieldRoot> cut = Render(p => p.Add(x => x.DefaultValue, 5).Add(x => x.LargeStep, 5));

        await test.ClickAsync(cut, cut.FindComponent<NumberFieldIncrement>().Instance, shiftKey: true);

        Assert.Equal("10", test.Text);
    }

    [Fact(DisplayName = "should not use the `smallStep` prop if no longer holding the alt key")]
    [Trait("Requirement", "NUM-STA-16")]
    public async Task Should_not_use_the_smallStep_prop_if_no_longer_holding_alt()
    {
        IRenderedComponent<NumberFieldRoot> cut = Render(p => p.Add(x => x.DefaultValue, 5));
        NumberFieldIncrement increment = cut.FindComponent<NumberFieldIncrement>().Instance;

        await test.ClickAsync(cut, increment, altKey: true);
        Assert.Equal("5.1", test.Text);

        await test.ClickAsync(cut, increment);
        Assert.Equal("6.1", test.Text);
    }

    [Fact(DisplayName = "reformats the visible text when the format prop changes at the same value")]
    [Trait("Requirement", "NUM-API-11")]
    [Trait("Requirement", "NUM-STA-06")]
    [Trait("Requirement", "NUM-I18N-13")]
    public void Reformats_the_visible_text_when_the_format_prop_changes_at_the_same_value()
    {
        IRenderedComponent<NumberFieldRoot> cut = Render(p => p.Add(x => x.Value, 1000));
        Assert.Equal(NumberFormatter.Get(s_enUs, null).Format(1000), test.Text);

        NumberFormatOptions usd = new() { Style = NumberFormatStyle.Currency, Currency = "USD" };
        cut.Render(p => p.Add(x => x.Value, 1000).Add(x => x.Format, usd));

        Assert.Equal("$1,000.00", test.Text);
        Assert.Equal(NumberFormatter.Get(s_enUs, usd).Format(1000), test.Text);
    }

    [Fact(DisplayName = "should format the value using the provided options")]
    [Trait("Requirement", "NUM-API-11")]
    [Trait("Requirement", "NUM-STA-05")]
    [Trait("Requirement", "NUM-I18N-01")]
    public void Should_format_the_value_using_the_provided_options()
    {
        IRenderedComponent<NumberFieldRoot> cut = Render(p => p
            .Add(x => x.DefaultValue, 1000)
            .Add(x => x.Format, new NumberFormatOptions { Style = NumberFormatStyle.Currency, Currency = "USD" }));

        Assert.Equal("$1,000.00", test.Text);
    }

    [Fact(DisplayName = "reflects controlled value changes in the textbox")]
    [Trait("Requirement", "NUM-STA-04")]
    public void Reflects_controlled_value_changes_in_the_textbox()
    {
        IRenderedComponent<NumberFieldRoot> cut = Render(p => p.Add(x => x.Value, 1));
        Assert.Equal("1", test.Text);

        cut.Render(p => p.Add(x => x.Value, 1234));

        Assert.Equal(1234.ToString("N0", s_enUs), test.Text);
    }

    [Fact(DisplayName = "should set the inputMode to numeric")]
    [Trait("Requirement", "NUM-STA-19")]
    [Trait("Requirement", "NUM-DOM-08")]
    public void Should_set_the_inputMode_to_numeric()
    {
        IRenderedComponent<NumberFieldRoot> cut = Render();

        Assert.Equal("numeric", Input(cut).GetAttribute("inputmode"));
    }

    [Fact(DisplayName = "should set the locale of the input")]
    [Trait("Requirement", "NUM-API-12")]
    [Trait("Requirement", "NUM-I18N-01")]
    public void Should_set_the_locale_of_the_input()
    {
        CultureInfo deDe = CultureInfo.GetCultureInfo("de-DE");
        IRenderedComponent<NumberFieldRoot> cut = Render(p => p.Add(x => x.DefaultValue, 1000.5), deDe);

        Assert.Equal("1.000,5", test.Text);
    }

    [Fact(DisplayName = "should use the default locale if no locale is provided")]
    [Trait("Requirement", "NUM-API-12")]
    public void Should_use_the_default_locale_if_no_locale_is_provided()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
        try
        {
            IRenderedComponent<NumberFieldRoot> cut = test.Context.Render<NumberFieldRoot>(p => p.Add(x => x.DefaultValue, 1000.5).Add(x => x.ChildContent, Parts));

            Assert.Equal("1.000,5", test.Text);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact(DisplayName = "should handle locales using space as the thousands separator")]
    [Trait("Requirement", "NUM-I18N-01")]
    [Trait("Requirement", "NUM-I18N-08")]
    public async Task Should_handle_locales_using_space_as_the_thousands_separator()
    {
        CultureInfo pl = CultureInfo.GetCultureInfo("pl");
        IRenderedComponent<NumberFieldRoot> cut = Render(p => p.Add(x => x.DefaultValue, 12345.5), pl);
        Assert.Equal("12 345,5", test.Text);

        await test.ClickAsync(cut, cut.FindComponent<NumberFieldIncrement>().Instance);

        Assert.Equal("12 346,5", test.Text);
    }

    [Fact(DisplayName = "has role prop")]
    [Trait("Requirement", "NUM-STR-04")]
    [Trait("Requirement", "NUM-ARIA-09")]
    public void Has_role_prop()
    {
        IRenderedComponent<NumberFieldRoot> cut = Render();

        IElement group = cut.Find("[role=group]");
        Assert.False(group.HasAttribute("id"));
        Assert.Equal("INPUT", group.FirstElementChild!.TagName);
    }

    [Fact(DisplayName = "has textbox role")]
    [Trait("Requirement", "NUM-ARIA-01")]
    [Trait("Requirement", "NUM-DOM-08")]
    public void Has_textbox_role()
    {
        IRenderedComponent<NumberFieldRoot> cut = Render();
        IElement input = Input(cut);

        Assert.Equal("text", input.GetAttribute("type"));
        Assert.False(input.HasAttribute("role"));
        Assert.Equal("Number field", input.GetAttribute("aria-roledescription"));
        Assert.Equal("off", input.GetAttribute("autocomplete"));
        Assert.Equal("off", input.GetAttribute("autocorrect"));
        Assert.Equal("false", input.GetAttribute("spellcheck"));
        Assert.False(input.HasAttribute("aria-valuenow"));
    }

    // NUM-API-22: a consumer value from the unmatched attributes replaces the default.
    [Fact(DisplayName = "port: a consumer aria-roledescription replaces the default")]
    [Trait("Requirement", "NUM-API-22")]
    [Trait("Requirement", "NUM-DOM-15")]
    public void A_consumer_aria_roledescription_replaces_the_default()
    {
        IRenderedComponent<NumberFieldRoot> cut = test.Context.Render<NumberFieldRoot>(p => p
            .Add(x => x.Culture, s_enUs)
            .Add(x => x.ChildContent, (RenderFragment)(b =>
            {
                b.OpenComponent<NumberFieldInput>(0);
                b.AddComponentParameter(1, "aria-roledescription", "Quantity");
                b.CloseComponent();
            })));

        Assert.Equal("Quantity", Input(cut).GetAttribute("aria-roledescription"));
    }

    [Fact(DisplayName = "throws a descriptive error when rendered outside <NumberField.Root>")]
    [Trait("Requirement", "NUM-STR-02")]
    [Trait("Requirement", "NUM-EDGE-01")]
    public void Throws_a_descriptive_error_when_rendered_outside_the_root()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => test.Context.Render<NumberFieldInput>());

        Assert.Equal("Blazix: `NumberFieldRootContext` is missing. NumberField parts must be placed within `<NumberFieldRoot>`.", error.Message);
        Assert.Throws<InvalidOperationException>(() => test.Context.Render<NumberFieldGroup>());
        Assert.Throws<InvalidOperationException>(() => test.Context.Render<NumberFieldIncrement>());
        Assert.Throws<InvalidOperationException>(() => test.Context.Render<NumberFieldDecrement>());
    }

    // NUM-API-18, NUM-DOM-09: the root's Id goes to the visible input and never to the root element or the hidden input.
    [Fact(DisplayName = "port: the root's id is the visible input's")]
    [Trait("Requirement", "NUM-API-18")]
    [Trait("Requirement", "NUM-DOM-09")]
    public void The_roots_id_is_the_visible_inputs()
    {
        IRenderedComponent<NumberFieldRoot> cut = Render(p => p.Add(x => x.Id, "quantity"));

        Assert.Equal("quantity", Input(cut).GetAttribute("id"));
        Assert.False(cut.Find("div").HasAttribute("id"));
        Assert.False(HiddenInput(cut).HasAttribute("id"));
        Assert.Equal("quantity", cut.Find("button[aria-label=Increase]").GetAttribute("aria-controls"));
    }

    // ADR 08 Amendments 2026-11-18 (decision 10 (a)); issue 277 design point 3: the shorthand's Razor form, and Step's numeric
    // literal through StepValue's conversion from double (the probe line of ADR 08 Amendments 2026-11-03).
    [Fact(DisplayName = "port: Format takes a format string written as an expression, and Step a numeric literal")]
    [Trait("Requirement", "NUM-API-11")]
    [Trait("Requirement", "NUM-API-06")]
    public void Format_takes_a_format_string_written_as_an_expression_and_Step_a_numeric_literal()
    {
        IRenderedComponent<NumberFieldFormatHost> cut = test.Context.Render<NumberFieldFormatHost>();

        IReadOnlyList<IElement> inputs = cut.FindAll("input[type=text]");
        Assert.Equal("1,234.50", inputs[0].GetAttribute("value"));
        Assert.Equal("25.6%", inputs[1].GetAttribute("value"));
        Assert.Equal("2.5", cut.FindAll("input[type=number]")[2].GetAttribute("step"));
    }

    // NUM-STA-03 and NUM-EDGE-04 (Test: none in the document): ownership is fixed at the first render, and a later switch
    // only warns, through Controlled<double?>("NumberField", "value", logger).
    [Fact(DisplayName = "port: ownership is fixed at the first render and a switch to controlled only warns")]
    [Trait("Requirement", "NUM-STA-03")]
    [Trait("Requirement", "NUM-EDGE-04")]
    public async Task Ownership_is_fixed_at_the_first_render_and_a_switch_to_controlled_only_warns()
    {
        IRenderedComponent<NumberFieldRoot> cut = Render(p => p.Add(x => x.DefaultValue, 1));

        cut.Render(p => p.Add(x => x.Value, 7));

        Assert.Equal("1", test.Text);
        Assert.Contains(test.Logger.Entries, e => e.Message.StartsWith("A component is changing the uncontrolled value state of NumberField to be controlled.", StringComparison.Ordinal));

        await test.ClickAsync(cut, cut.FindComponent<NumberFieldIncrement>().Instance);
        Assert.Equal("2", test.Text);
    }

    // NUM-EVT-03 (Test: none in the document): a step's details carry its direction; the seed of an empty field has none.
    [Fact(DisplayName = "port: a step carries its direction and the first step of an empty field none")]
    [Trait("Requirement", "NUM-EVT-03")]
    public async Task A_step_carries_its_direction_and_the_first_step_of_an_empty_field_none()
    {
        List<int?> directions = [];
        IRenderedComponent<NumberFieldRoot> cut = Render(p => p.Add(x => x.OnValueChange, (ValueChangeEventArgs<double?> e) => directions.Add(((NumberFieldChangeEventDetails)e.Details).Direction)));

        await test.ClickAsync(cut, cut.FindComponent<NumberFieldDecrement>().Instance);
        await test.ClickAsync(cut, cut.FindComponent<NumberFieldDecrement>().Instance);
        await test.ClickAsync(cut, cut.FindComponent<NumberFieldIncrement>().Instance);

        Assert.Equal([null, -1, 1], directions);
        Assert.Equal("0", test.Text);
    }

    // NUM-STA-05/06, design point 7 (a): the first text is rendered and handed to the module, which writes C#'s answers;
    // the answer to a blur echoes the blur's revision, so the module never applies an older text over a newer one.
    [Fact(DisplayName = "port: the input's text is handed to the module at attach, and the answer to a blur echoes its revision")]
    [Trait("Requirement", "NUM-STA-05")]
    [Trait("Requirement", "NUM-STA-06")]
    [Trait("Requirement", "NUM-DOM-08")]
    public async Task The_inputs_text_is_handed_to_the_module_and_the_answer_to_a_blur_echoes_its_revision()
    {
        IRenderedComponent<NumberFieldRoot> cut = Render(p => p.Add(x => x.DefaultValue, 1234.5));
        Assert.Equal("1,234.5", Input(cut).GetAttribute("value"));
        Assert.Equal("""{"value":"1,234.5","revision":0}""", Assert.Single(test.NumberFieldOptions));

        await test.ClickAsync(cut, cut.FindComponent<NumberFieldIncrement>().Instance);
        Assert.Equal("""{"value":"1,235.5","revision":0}""", test.NumberFieldOptions[^1]);
        Assert.Equal("1,234.5", Input(cut).GetAttribute("value"));

        await test.BlurAsync(cut, "1,235.5", revision: 3);
        Assert.Equal("""{"value":"1,235.5","revision":3}""", test.NumberFieldOptions[^1]);
    }

    // NUM-PTR-02, ADR 01's C#-origin veto row; design point 1 (a): on WebAssembly a consumer's onpointerdown runs inside the
    // hold-start decision, where PreventPartHandler() keeps the press from acting, and Blazor's dispatch of the same press does
    // not run it again; on Server it runs from the part's own handler.
    [Fact(DisplayName = "port: a consumer onpointerdown that prevents the part's handler at hold-start vetoes the press once")]
    [Trait("Requirement", "NUM-PTR-02")]
    [Trait("Requirement", "NUM-API-25")]
    public async Task A_consumer_onpointerdown_that_prevents_the_parts_handler_at_hold_start_vetoes_the_press_once()
    {
        int calls = 0;
        IRenderedComponent<NumberFieldRoot> cut = test.Context.Render<NumberFieldRoot>(p => p
            .Add(x => x.Culture, s_enUs)
            .Add(x => x.DefaultValue, 0)
            .Add(x => x.ChildContent, (RenderFragment)(b =>
            {
                b.OpenComponent<NumberFieldIncrement>(0);
                b.AddComponentParameter(1, "onpointerdown", EventCallback.Factory.Create<Microsoft.AspNetCore.Components.Web.PointerEventArgs>(this, e =>
                {
                    calls++;
                    e.PreventPartHandler();
                }));
                b.CloseComponent();
            })));
        Blazix.Primitives.Interop.ButtonTwin twin = test.ButtonTwin(cut.FindComponent<NumberFieldIncrement>().Instance);

        string verdict = await cut.InvokeAsync(() => twin.Decide(Blazix.Primitives.Interop.ButtonTwin.HoldStartPoint, NumberFieldTestContext.EventJson("pointerdown")));
        await cut.Find("button").TriggerEventAsync("onpointerdown", new Microsoft.AspNetCore.Components.Web.PointerEventArgs { Type = "pointerdown" });

        Assert.Equal("stop", verdict);
        Assert.Equal(1, calls);

        // A press no decision asked about (Server) still runs the consumer's handler, from the part's own.
        await cut.Find("button").TriggerEventAsync("onpointerdown", new Microsoft.AspNetCore.Components.Web.PointerEventArgs { Type = "pointerdown" });
        Assert.Equal(2, calls);
    }

    // NUM-PTR-02 under ADR 01's consumer-veto row, measured 2026-11-19: on Server the press has already started the hold when
    // the consumer's veto arrives, so the part stops the sequence then (the hold's stop counter), as it does at a boundary;
    // a press the consumer did not veto keeps its hold.
    [Fact(DisplayName = "port: a consumer veto that arrives after the press started stops the hold")]
    [Trait("Requirement", "NUM-PTR-02")]
    public async Task A_consumer_veto_that_arrives_after_the_press_started_stops_the_hold()
    {
        bool veto = false;
        IRenderedComponent<NumberFieldRoot> cut = test.Context.Render<NumberFieldRoot>(p => p
            .Add(x => x.Culture, s_enUs)
            .Add(x => x.DefaultValue, 0)
            .Add(x => x.ChildContent, (RenderFragment)(b =>
            {
                b.OpenComponent<NumberFieldIncrement>(0);
                b.AddComponentParameter(1, "onpointerdown", EventCallback.Factory.Create<Microsoft.AspNetCore.Components.Web.PointerEventArgs>(this, e =>
                {
                    if (veto)
                    {
                        e.PreventPartHandler();
                    }
                }));
                b.CloseComponent();
            })));

        await cut.Find("button").TriggerEventAsync("onpointerdown", new Microsoft.AspNetCore.Components.Web.PointerEventArgs { Type = "pointerdown" });
        Assert.DoesNotContain(test.ButtonUpdates, update => update.Contains("\"stop\"", StringComparison.Ordinal));

        veto = true;
        await cut.Find("button").TriggerEventAsync("onpointerdown", new Microsoft.AspNetCore.Components.Web.PointerEventArgs { Type = "pointerdown" });
        Assert.Contains("\"stop\":1", test.ButtonUpdates[^1], StringComparison.Ordinal);
    }

    private static RenderFragment Parts => builder =>
    {
        builder.OpenComponent<NumberFieldGroup>(0);
        builder.AddComponentParameter(1, nameof(NumberFieldGroup.ChildContent), (RenderFragment)(group =>
        {
            group.OpenComponent<NumberFieldInput>(0);
            group.CloseComponent();
            group.OpenComponent<NumberFieldIncrement>(1);
            group.CloseComponent();
            group.OpenComponent<NumberFieldDecrement>(2);
            group.CloseComponent();
        }));
        builder.CloseComponent();
    };

    private IRenderedComponent<NumberFieldRoot> Render(Action<ComponentParameterCollectionBuilder<NumberFieldRoot>>? parameters = null, CultureInfo? culture = null)
    {
        return test.Context.Render<NumberFieldRoot>(p =>
        {
            p.Add(x => x.Culture, culture ?? s_enUs);
            p.Add(x => x.ChildContent, Parts);
            parameters?.Invoke(p);
        });
    }

    private static IElement Input(IRenderedComponent<NumberFieldRoot> cut) => cut.Find("input[type=text]");

    private static IElement HiddenInput(IRenderedComponent<NumberFieldRoot> cut) => cut.Find("input[aria-hidden][type=number]");
}
