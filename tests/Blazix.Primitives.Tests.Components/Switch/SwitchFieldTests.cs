using System.Linq.Expressions;
using System.Text.Json;
using AngleSharp.Dom;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// SwitchRoot.test.tsx's "Field" block, the Field-rendering titles of its "prop: onCheckedChange", "Form" and label blocks,
// and the ADR 05 item 5 participation (issue 76), on a switch inside a field (issue 80's follow-up). A toggle reaches C# as
// the button module's report; the field's native validity is the hidden input's, read through the form module's refresh
// (SettleFormAsync); a blur is the form module's report on the root; a submission is the form element's report.
/// <summary>A switch inside a field: registration, the field's flags, validation, labels, descriptions, errors and the form.</summary>
public sealed class SwitchFieldTests : IDisposable
{
    private const string Switch = "[data-testid=switch]";

    private readonly SwitchTestContext test = new();

    private BunitContext Context => test.Context;

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "should receive disabled prop from Field.Root")]
    [Trait("Requirement", "SWT-API-04")]
    [Trait("Requirement", "SWT-DOM-03")]
    public void Should_receive_disabled_prop_from_Field_Root()
    {
        IRenderedComponent<SwitchFieldHost> cut = Context.Render<SwitchFieldHost>(p => p.Add(x => x.FieldDisabled, true));

        Assert.Equal(string.Empty, cut.Find(Switch).GetAttribute("data-disabled"));
        Assert.Equal("true", cut.Find(Switch).GetAttribute("aria-disabled"));
        Assert.True(cut.Find("input[type=checkbox]").HasAttribute("disabled"));
    }

    [Fact(DisplayName = "port: a switch inside a disabled Field.Item is not disabled by it, and claims the item's label")]
    [Trait("Requirement", "SWT-API-04")]
    public void Port_a_switch_inside_a_disabled_Field_Item_is_not_disabled_by_it()
    {
        // SWT-API-04: `fieldDisabled || disabled`; Base UI's Switch.Root reads the field root's disabled state only (a
        // Field.Item disables a checkbox, not a switch), and takes the item's labelable scope.
        IRenderedComponent<FieldRoot> cut = Context.Render<FieldRoot>(p => p.AddChildContent<FieldItem>(item => item
            .Add(x => x.Disabled, true)
            .AddChildContent(builder =>
            {
                builder.OpenComponent<FieldLabel>(0);
                builder.AddComponentParameter(1, "data-testid", "item-label");
                builder.CloseComponent();
                builder.OpenComponent<SwitchRoot>(2);
                builder.AddComponentParameter(3, "data-testid", "switch");
                builder.CloseComponent();
            })));

        Assert.False(cut.Find(Switch).HasAttribute("aria-disabled"));
        Assert.False(cut.Find(Switch).HasAttribute("data-disabled"));
        Assert.False(cut.Find("input[type=checkbox]").HasAttribute("disabled"));
        Assert.Equal(cut.Find("input[type=checkbox]").GetAttribute("id"), cut.Find("[data-testid=item-label]").GetAttribute("for"));
    }

    [Fact(DisplayName = "should receive name prop from Field.Root")]
    [Trait("Requirement", "SWT-API-07")]
    [Trait("Requirement", "SWT-DOM-08")]
    public void Should_receive_name_prop_from_Field_Root()
    {
        IRenderedComponent<SwitchFieldHost> cut = Context.Render<SwitchFieldHost>(p => p.Add(x => x.FieldName, "field-switch").Add(x => x.SwitchName, "own"));

        Assert.Equal("field-switch", cut.Find("input[type=checkbox]").GetAttribute("name"));
    }

    [Fact(DisplayName = "[data-touched]")]
    [Trait("Requirement", "SWT-FRM-14")]
    [Trait("Requirement", "SWT-DOM-04")]
    public async Task Data_touched()
    {
        IRenderedComponent<SwitchFieldHost> cut = Context.Render<SwitchFieldHost>();
        Assert.False(cut.Find(Switch).HasAttribute("data-touched"));

        await cut.Find(Switch).FocusAsync(new FocusEventArgs());
        await test.BlurAsync(cut);

        Assert.Equal(string.Empty, cut.Find(Switch).GetAttribute("data-touched"));
    }

    [Fact(DisplayName = "[data-dirty]")]
    [Trait("Requirement", "SWT-STA-07")]
    [Trait("Requirement", "SWT-FRM-07")]
    [Trait("Requirement", "SWT-DOM-04")]
    public async Task Data_dirty()
    {
        IRenderedComponent<SwitchFieldHost> cut = Context.Render<SwitchFieldHost>();
        Assert.False(cut.Find(Switch).HasAttribute("data-dirty"));

        await test.ToggleAsync(cut, true);
        await test.SettleFormAsync(cut);
        Assert.Equal(string.Empty, cut.Find(Switch).GetAttribute("data-dirty"));

        // Port: back to the baseline captured at the first registration clears it (SWT-STA-07's last clause).
        await test.ToggleAsync(cut, false);
        await test.SettleFormAsync(cut);
        Assert.False(cut.Find(Switch).HasAttribute("data-dirty"));
    }

    [Fact(DisplayName = "adds [data-filled] attribute when checked after being initially unchecked")]
    [Trait("Requirement", "SWT-STA-06")]
    [Trait("Requirement", "SWT-DOM-04")]
    public async Task Adds_data_filled_attribute_when_checked_after_being_initially_unchecked()
    {
        IRenderedComponent<SwitchFieldHost> cut = Context.Render<SwitchFieldHost>();
        Assert.False(cut.Find(Switch).HasAttribute("data-filled"));

        await test.ToggleAsync(cut, true);
        await test.SettleFormAsync(cut);
        Assert.Equal(string.Empty, cut.Find(Switch).GetAttribute("data-filled"));

        await test.ToggleAsync(cut, false);
        await test.SettleFormAsync(cut);
        Assert.False(cut.Find(Switch).HasAttribute("data-filled"));
    }

    [Fact(DisplayName = "clears [data-filled] when a controlled switch remounts unchecked")]
    [Trait("Requirement", "SWT-STA-06")]
    public void Clears_data_filled_when_a_controlled_switch_remounts_unchecked()
    {
        IRenderedComponent<SwitchFieldHost> cut = Context.Render<SwitchFieldHost>(p => p
            .Add(x => x.Controlled, true)
            .Add(x => x.IgnoreChanges, true)
            .Add(x => x.Value, true)
            .Add(x => x.SwitchKey, "false"));
        Assert.Equal(string.Empty, cut.Find("[data-testid=root]").GetAttribute("data-filled"));

        cut.Render(p => p.Add(x => x.Value, false).Add(x => x.SwitchKey, "true"));

        Assert.False(cut.Find("[data-testid=root]").HasAttribute("data-filled"));
    }

    [Fact(DisplayName = "removes [data-filled] attribute when unchecked after being initially checked")]
    [Trait("Requirement", "SWT-STA-06")]
    public async Task Removes_data_filled_attribute_when_unchecked_after_being_initially_checked()
    {
        IRenderedComponent<SwitchFieldHost> cut = Context.Render<SwitchFieldHost>(p => p.Add(x => x.DefaultChecked, true));
        Assert.Equal(string.Empty, cut.Find(Switch).GetAttribute("data-filled"));

        await test.ToggleAsync(cut, false);
        await test.SettleFormAsync(cut);

        Assert.False(cut.Find(Switch).HasAttribute("data-filled"));
    }

    [Fact(DisplayName = "[data-focused]")]
    [Trait("Requirement", "SWT-FOC-04")]
    [Trait("Requirement", "SWT-DOM-04")]
    public async Task Data_focused()
    {
        IRenderedComponent<SwitchFieldHost> cut = Context.Render<SwitchFieldHost>();
        Assert.False(cut.Find(Switch).HasAttribute("data-focused"));

        await cut.Find(Switch).FocusAsync(new FocusEventArgs());
        Assert.Equal(string.Empty, cut.Find(Switch).GetAttribute("data-focused"));

        await test.BlurAsync(cut);
        Assert.False(cut.Find(Switch).HasAttribute("data-focused"));
    }

    [Fact(DisplayName = "does not set [data-focused] when disabled")]
    [Trait("Requirement", "SWT-FOC-04")]
    [Trait("Requirement", "SWT-FOC-02")]
    public async Task Does_not_set_data_focused_when_disabled()
    {
        IRenderedComponent<SwitchFieldHost> cut = Context.Render<SwitchFieldHost>(p => p.Add(x => x.SwitchDisabled, true));

        await cut.Find(Switch).FocusAsync(new FocusEventArgs());

        Assert.False(cut.Find(Switch).HasAttribute("data-focused"));
        Assert.Equal("-1", cut.Find(Switch).GetAttribute("tabindex"));
    }

    [Fact(DisplayName = "is removed when the focused switch becomes disabled")]
    [Trait("Requirement", "SWT-FOC-05")]
    public async Task Is_removed_when_the_focused_switch_becomes_disabled()
    {
        IRenderedComponent<SwitchFieldHost> cut = Context.Render<SwitchFieldHost>();
        await cut.Find(Switch).FocusAsync(new FocusEventArgs());
        Assert.Equal(string.Empty, cut.Find("[data-testid=root]").GetAttribute("data-focused"));

        cut.Render(p => p.Add(x => x.SwitchDisabled, true));

        Assert.False(cut.Find("[data-testid=root]").HasAttribute("data-focused"));
        Assert.False(cut.Find(Switch).HasAttribute("data-focused"));
    }

    [Fact(DisplayName = "is removed when the focused switch unmounts")]
    [Trait("Requirement", "SWT-FOC-05")]
    public async Task Is_removed_when_the_focused_switch_unmounts()
    {
        IRenderedComponent<SwitchFieldHost> cut = Context.Render<SwitchFieldHost>();
        await cut.Find(Switch).FocusAsync(new FocusEventArgs());

        cut.Render(p => p.Add(x => x.SwitchMounted, false));

        Assert.False(cut.Find("[data-testid=root]").HasAttribute("data-focused"));
    }

    [Fact(DisplayName = "does not change state when canceled via a root click")]
    [Trait("Requirement", "SWT-EVT-04")]
    public async Task Does_not_change_state_when_canceled_via_a_root_click()
    {
        IRenderedComponent<SwitchFieldHost> cut = Context.Render<SwitchFieldHost>(p => p.Add(x => x.OnCheckedChange, args => args.Details.Cancel()));

        // WebAssembly: the decision inside the click answers stop, so the box is reverted natively.
        Assert.Equal("stop", await test.DecideToggleAsync(cut));
        await test.SettleFormAsync(cut);

        Assert.Equal("false", cut.Find(Switch).GetAttribute("aria-checked"));
        Assert.False(cut.Find(Switch).HasAttribute("data-dirty"));
        Assert.False(cut.Find(Switch).HasAttribute("data-filled"));
    }

    [Fact(DisplayName = "does not change state when canceled via a hidden input click")]
    [Trait("Requirement", "SWT-EVT-04")]
    [Trait("Requirement", "SWT-DOM-08")]
    public async Task Does_not_change_state_when_canceled_via_a_hidden_input_click()
    {
        IRenderedComponent<SwitchFieldHost> cut = Context.Render<SwitchFieldHost>(p => p.Add(x => x.OnCheckedChange, args => args.Details.Cancel()));

        // Server: the report arrives after the browser flipped the box, so the input is restored through the dom primitive.
        await test.ToggleAsync(cut, true);
        await test.SettleFormAsync(cut);

        Assert.Equal("false", cut.Find(Switch).GetAttribute("aria-checked"));
        Assert.Equal("""{"checked":false,"checkedRestores":1,"liveRegion":false}""", test.DomOptions[^1]);
        Assert.False(cut.Find(Switch).HasAttribute("data-dirty"));
        Assert.False(cut.Find(Switch).HasAttribute("data-filled"));
    }

    [Fact(DisplayName = "prop: validationMode=onChange")]
    [Trait("Requirement", "SWT-FRM-09")]
    public async Task Prop_validationMode_onChange()
    {
        IRenderedComponent<SwitchFieldHost> cut = Context.Render<SwitchFieldHost>(p => p
            .Add(x => x.ValidationMode, ValidationMode.OnChange)
            .Add(x => x.Validate, args => ValueTask.FromResult<FieldValidationResult?>((bool)args.Value! ? "error" : null)));
        Assert.False(cut.Find(Switch).HasAttribute("aria-invalid"));

        await test.ToggleAsync(cut, true);
        await test.SettleFormAsync(cut);

        Assert.Equal("true", cut.Find(Switch).GetAttribute("aria-invalid"));
    }

    [Fact(DisplayName = "validates once when changed by the user")]
    [Trait("Requirement", "SWT-FRM-09")]
    [Trait("Requirement", "SWT-FRM-06")]
    public async Task Validates_once_when_changed_by_the_user()
    {
        List<object?> values = [];
        IRenderedComponent<SwitchFieldHost> cut = Context.Render<SwitchFieldHost>(p => p
            .Add(x => x.ValidationMode, ValidationMode.OnChange)
            .Add(x => x.Validate, args =>
            {
                values.Add(args.Value);
                return ValueTask.FromResult<FieldValidationResult?>(null);
            }));

        await test.ToggleAsync(cut, true);
        await test.SettleFormAsync(cut);

        Assert.Equal([true], values);
    }

    [Fact(DisplayName = "revalidates when a controlled value changes externally")]
    [Trait("Requirement", "SWT-FRM-10")]
    [Trait("Requirement", "SWT-EVT-05")]
    public async Task Revalidates_when_a_controlled_value_changes_externally()
    {
        List<object?> values = [];
        int changes = 0;
        IRenderedComponent<SwitchFieldHost> cut = Context.Render<SwitchFieldHost>(p => p
            .Add(x => x.FieldName, "newsletters")
            .Add(x => x.ValidationMode, ValidationMode.OnChange)
            .Add(x => x.Validate, args =>
            {
                values.Add(args.Value);
                return ValueTask.FromResult<FieldValidationResult?>((bool)args.Value! ? "error" : null);
            })
            .Add(x => x.Controlled, true)
            .Add(x => x.Value, false)
            .Add(x => x.OnCheckedChange, _ => changes++));
        Assert.False(cut.Find(Switch).HasAttribute("aria-invalid"));
        int initial = values.Count;

        cut.Render(p => p.Add(x => x.Value, true));
        await test.SettleFormAsync(cut);

        Assert.Equal(initial + 1, values.Count);
        Assert.Equal(true, values[^1]);
        Assert.Equal("true", cut.Find(Switch).GetAttribute("aria-invalid"));
        // SWT-EVT-05: a change the consumer made is not a user toggle.
        Assert.Equal(0, changes);
    }

    [Fact(DisplayName = "prop: validationMode=onBlur")]
    [Trait("Requirement", "SWT-FRM-11")]
    public async Task Prop_validationMode_onBlur()
    {
        IRenderedComponent<SwitchFieldHost> cut = Context.Render<SwitchFieldHost>(p => p
            .Add(x => x.ShowError, true)
            .Add(x => x.ValidationMode, ValidationMode.OnBlur)
            .Add(x => x.Validate, args => ValueTask.FromResult<FieldValidationResult?>((bool)args.Value! ? "error" : null)));
        Assert.False(cut.Find(Switch).HasAttribute("aria-invalid"));

        await test.ToggleAsync(cut, true);
        await test.SettleFormAsync(cut);
        Assert.False(cut.Find(Switch).HasAttribute("aria-invalid"));

        await test.BlurAsync(cut);

        Assert.Equal("true", cut.Find(Switch).GetAttribute("aria-invalid"));
    }

    [Fact(DisplayName = "prop: validationMode=onSubmit")]
    [Trait("Requirement", "SWT-FRM-12")]
    [Trait("Requirement", "SWT-ARIA-11")]
    [Trait("Requirement", "SWT-FRM-06")]
    public async Task Prop_validationMode_onSubmit()
    {
        IRenderedComponent<SwitchFieldHost> cut = Context.Render<SwitchFieldHost>(p => p.Add(x => x.InForm, true).Add(x => x.ShowError, true).Add(x => x.Required, true));
        Assert.False(cut.Find(Switch).HasAttribute("aria-invalid"));

        // The hidden input is required and unchecked: the module reads valueMissing, then the form is submitted.
        test.InputValidity = Native.ValueMissing;
        await test.SyncInputAsync(cut);
        await SubmitAsync(cut);
        Assert.Equal("true", cut.Find(Switch).GetAttribute("aria-invalid"));
        Assert.NotEmpty(cut.FindAll("[data-testid=error]"));

        // After the first submit every toggle validates again.
        test.InputValidity = NativeValidity.Default;
        await test.ToggleAsync(cut, true);
        await test.SettleFormAsync(cut);
        Assert.False(cut.Find(Switch).HasAttribute("aria-invalid"));

        test.InputValidity = Native.ValueMissing;
        await test.ToggleAsync(cut, false);
        await test.SettleFormAsync(cut);
        Assert.Equal("true", cut.Find(Switch).GetAttribute("aria-invalid"));
    }

    [Fact(DisplayName = "port: a required switch is invalid at its first submit, before any toggle")]
    [Trait("Requirement", "SWT-FRM-08")]
    [Trait("Requirement", "SWT-API-06")]
    public async Task Port_a_required_switch_is_invalid_at_its_first_submit()
    {
        // The field holds the hidden input's native state from the attach on, not only after a change refreshed it: the
        // module reads valueMissing for a required unchecked input, which the submit then reports (triggers native HTML
        // validation on submit, whose browser half is tests/browser/switch.spec.ts).
        test.InputValidity = Native.ValueMissing;
        IRenderedComponent<SwitchFieldHost> cut = Context.Render<SwitchFieldHost>(p => p.Add(x => x.InForm, true).Add(x => x.ShowError, true).Add(x => x.Required, true));
        await test.SettleFormAsync(cut);

        await SubmitAsync(cut);

        Assert.Equal("true", cut.Find(Switch).GetAttribute("aria-invalid"));
        Assert.NotEmpty(cut.FindAll("[data-testid=error]"));
    }

    [Fact(DisplayName = "port: a required native-button switch is invalid at its first submit, before any toggle")]
    [Trait("Requirement", "SWT-FRM-08")]
    public async Task Port_a_required_native_button_switch_is_invalid_at_its_first_submit()
    {
        // Under nativeButton the hidden input renders no id, so the state the module reads at submit cannot reach it by
        // id: the field validates from the input's last report, which the input gives once when its handle attaches.
        test.InputValidity = Native.ValueMissing;
        IRenderedComponent<SwitchFieldHost> cut = Context.Render<SwitchFieldHost>(p => p.Add(x => x.InForm, true).Add(x => x.ShowError, true).Add(x => x.Required, true).Add(x => x.NativeButton, true));
        await test.SettleFormAsync(cut);

        await SubmitAsync(cut);

        Assert.Equal("true", cut.Find(Switch).GetAttribute("aria-invalid"));
    }

    [Fact(DisplayName = "clears external errors on change")]
    [Trait("Requirement", "SWT-FRM-13")]
    [Trait("Requirement", "SWT-ARIA-11")]
    public async Task Clears_external_errors_on_change()
    {
        FormErrors errors = new() { ["test"] = "test" };
        IRenderedComponent<SwitchFieldHost> cut = Context.Render<SwitchFieldHost>(p => p
            .Add(x => x.InForm, true)
            .Add(x => x.Errors, errors)
            .Add(x => x.FieldName, "test")
            .Add(x => x.ShowError, true));

        Assert.Equal("true", cut.Find(Switch).GetAttribute("aria-invalid"));
        Assert.Equal("test", cut.Find("[data-testid=error]").TextContent);

        await test.ToggleAsync(cut, true);
        await test.SettleFormAsync(cut);
        await test.CompleteExitsAsync(cut);

        Assert.False(cut.Find(Switch).HasAttribute("aria-invalid"));
        Assert.Empty(cut.FindAll("[data-testid=error]"));
    }

    [Fact(DisplayName = "port: the root and the hidden input carry aria-invalid while the field is invalid and enabled")]
    [Trait("Requirement", "SWT-ARIA-11")]
    [Trait("Requirement", "SWT-DOM-15")]
    [Trait("Requirement", "SWT-DOM-05")]
    public void Port_the_root_and_the_hidden_input_carry_aria_invalid()
    {
        FormErrors errors = new() { ["n"] = "bad" };
        IRenderedComponent<SwitchFieldHost> cut = Context.Render<SwitchFieldHost>(p => p.Add(x => x.InForm, true).Add(x => x.Errors, errors).Add(x => x.FieldName, "n"));
        Assert.Equal("true", cut.Find(Switch).GetAttribute("aria-invalid"));
        Assert.Equal("true", cut.Find("input[type=checkbox]").GetAttribute("aria-invalid"));
        Assert.Equal(string.Empty, cut.Find(Switch).GetAttribute("data-invalid"));

        cut.Render(p => p.Add(x => x.SwitchDisabled, true));
        Assert.False(cut.Find(Switch).HasAttribute("aria-invalid"));
        Assert.False(cut.Find("input[type=checkbox]").HasAttribute("aria-invalid"));
    }

    [Fact(DisplayName = "sets `for` on the label")]
    [Trait("Requirement", "SWT-FRM-16")]
    [Trait("Requirement", "SWT-ARIA-06")]
    [Trait("Requirement", "SWT-DOM-10")]
    public async Task Sets_for_on_the_label()
    {
        IRenderedComponent<SwitchFieldHost> cut = Context.Render<SwitchFieldHost>(p => p.Add(x => x.ShowLabel, true).Add(x => x.LabelWraps, true));

        IElement label = cut.Find("[data-testid=label]");
        string? @for = label.GetAttribute("for");
        Assert.NotNull(@for);
        Assert.Equal(cut.Find("input[type=checkbox]").GetAttribute("id"), @for);
        Assert.Equal(label.GetAttribute("id"), cut.Find(Switch).GetAttribute("aria-labelledby"));
        Assert.Equal("false", cut.Find(Switch).GetAttribute("aria-checked"));

        // The label's click is native: the browser clicks the input it points at (tests/browser/switch.spec.ts).
        await test.ToggleAsync(cut, true);
        Assert.Equal("true", cut.Find(Switch).GetAttribute("aria-checked"));
    }

    [Fact(DisplayName = "when the label is sibling to the switch")]
    [Trait("Requirement", "SWT-FRM-16")]
    [Trait("Requirement", "SWT-ARIA-06")]
    public async Task When_the_label_is_sibling_to_the_switch()
    {
        IRenderedComponent<SwitchFieldHost> cut = Context.Render<SwitchFieldHost>(p => p.Add(x => x.ShowLabel, true));

        IElement label = cut.Find("[data-testid=label]");
        Assert.NotNull(label.GetAttribute("for"));
        Assert.Equal(cut.Find("input[type=checkbox]").GetAttribute("id"), label.GetAttribute("for"));
        Assert.Equal(label.GetAttribute("id"), cut.Find(Switch).GetAttribute("aria-labelledby"));
        Assert.Equal("false", cut.Find(Switch).GetAttribute("aria-checked"));

        await test.ToggleAsync(cut, true);
        Assert.Equal("true", cut.Find(Switch).GetAttribute("aria-checked"));
    }

    [Fact(DisplayName = "when rendering a non-native button")]
    [Trait("Requirement", "SWT-FRM-16")]
    public void When_rendering_a_non_native_button()
    {
        IRenderedComponent<SwitchFieldHost> cut = Context.Render<SwitchFieldHost>(p => p.Add(x => x.ShowLabel, true).Add(x => x.SpanRender, true));

        IElement label = cut.Find("[data-testid=label]");
        Assert.NotNull(label.GetAttribute("for"));
        Assert.Equal(label.GetAttribute("for"), cut.Find("input[type=checkbox]").GetAttribute("id"));
        Assert.Equal(label.GetAttribute("id"), cut.Find(Switch).GetAttribute("aria-labelledby"));
    }

    [Fact(DisplayName = "when rendering a non-native label")]
    [Trait("Requirement", "SWT-PTR-10")]
    [Trait("Requirement", "SWT-ARIA-06")]
    public void When_rendering_a_non_native_label()
    {
        // The click half (a non-native label focuses the switch and does not toggle it) is tests/browser/switch.spec.ts.
        IRenderedComponent<SwitchFieldHost> cut = Context.Render<SwitchFieldHost>(p => p.Add(x => x.ShowLabel, true).Add(x => x.LabelWraps, true).Add(x => x.NativeLabel, false));

        IElement label = cut.Find("[data-testid=label]");
        Assert.Null(label.GetAttribute("for"));
        Assert.NotNull(label.GetAttribute("id"));
        Assert.Equal(label.GetAttribute("id"), cut.Find(Switch).GetAttribute("aria-labelledby"));
        Assert.Equal("false", cut.Find(Switch).GetAttribute("aria-checked"));
    }

    [Fact(DisplayName = "prefers `aria-label` over an associated label")]
    [Trait("Requirement", "SWT-ARIA-06")]
    [Trait("Requirement", "SWT-EDGE-07")]
    public void Prefers_aria_label_over_an_associated_label()
    {
        // The Field.Label half; the native label half is tests/browser/switch.spec.ts.
        IRenderedComponent<SwitchFieldHost> cut = Context.Render<SwitchFieldHost>(p => p.Add(x => x.ShowLabel, true).Add(x => x.LabelWraps, true).Add(x => x.AriaLabel, "Bluetooth"));
        Assert.False(cut.Find(Switch).HasAttribute("aria-labelledby"));
        Assert.Equal("Bluetooth", cut.Find(Switch).GetAttribute("aria-label"));

        // SWT-EDGE-07: a blank aria-label keeps the association.
        cut.Render(p => p.Add(x => x.AriaLabel, " \n\t "));
        Assert.Equal(cut.Find("[data-testid=label]").GetAttribute("id"), cut.Find(Switch).GetAttribute("aria-labelledby"));
    }

    [Fact(DisplayName = "Field.Description")]
    [Trait("Requirement", "SWT-ARIA-10")]
    [Trait("Requirement", "SWT-FRM-17")]
    [Trait("Requirement", "SWT-DOM-15")]
    public void Field_Description()
    {
        IRenderedComponent<SwitchFieldHost> cut = Context.Render<SwitchFieldHost>(p => p.Add(x => x.ShowDescription, true).Add(x => x.AriaDescribedBy, "external-description"));

        string descriptionId = cut.Find("[data-testid=description]").GetAttribute("id")!;
        Assert.Equal(descriptionId, cut.Find("input[type=checkbox]").GetAttribute("aria-describedby"));
        Assert.Equal($"external-description {descriptionId}", cut.Find(Switch).GetAttribute("aria-describedby"));
    }

    [Fact(DisplayName = "port: in native mode the root takes the field's control id and the label points at it")]
    [Trait("Requirement", "SWT-DOM-01")]
    [Trait("Requirement", "SWT-DOM-10")]
    [Trait("Requirement", "SWT-FRM-16")]
    public void Port_in_native_mode_the_root_takes_the_fields_control_id()
    {
        IRenderedComponent<SwitchFieldHost> cut = Context.Render<SwitchFieldHost>(p => p.Add(x => x.ShowLabel, true).Add(x => x.NativeButton, true));

        string? rootId = cut.Find(Switch).GetAttribute("id");
        Assert.False(string.IsNullOrEmpty(rootId));
        Assert.Equal(rootId, cut.Find("[data-testid=label]").GetAttribute("for"));
        Assert.False(cut.Find("input[type=checkbox]").HasAttribute("id"));
    }

    [Fact(DisplayName = "port: the switch registers as the field's control with its own name, and not while disabled")]
    [Trait("Requirement", "SWT-FRM-06")]
    [Trait("Requirement", "SWT-FRM-18")]
    public async Task Port_the_switch_registers_as_the_fields_control()
    {
        List<object?> values = [];
        IRenderedComponent<SwitchFieldHost> cut = Context.Render<SwitchFieldHost>(p => p
            .Add(x => x.SwitchName, "own-name")
            .Add(x => x.ValidationMode, ValidationMode.OnChange)
            .Add(x => x.Validate, args =>
            {
                values.Add(args.Value);
                return ValueTask.FromResult<FieldValidationResult?>(null);
            }));

        await test.ToggleAsync(cut, true);
        await test.SettleFormAsync(cut);
        Assert.Equal([true], values);

        // SWT-FRM-18: without a field name the switch's own name is the field's.
        Assert.Equal("own-name", cut.Find("input[type=checkbox]").GetAttribute("name"));

        // The hidden input is the field's validation input, attached with the root as its focus target (SWT-FOC-03).
        FormControlOptions input = test.FormOptions.Last(o => !o.ReportEvents);
        Assert.Equal(cut.Find(Switch).GetAttribute("id"), input.FocusTarget);

        cut.Render(p => p.Add(x => x.SwitchDisabled, true));
        await test.ToggleAsync(cut, false);
        Assert.Equal([true], values);
    }

    [Fact(DisplayName = "port: a root that is already focused when its tracking starts acquires the focused state")]
    [Trait("Requirement", "SWT-FOC-05")]
    public async Task Port_a_root_already_focused_when_its_tracking_starts_acquires_the_focused_state()
    {
        IRenderedComponent<SwitchFieldHost> cut = Context.Render<SwitchFieldHost>();

        await test.FocusedAtAttachAsync(cut);

        Assert.Equal(string.Empty, cut.Find("[data-testid=root]").GetAttribute("data-focused"));
    }

    [Fact(DisplayName = "port: CheckedExpression notifies the cascaded EditContext of every committed change and names the field")]
    [Trait("Verifies", "ADR05-5")]
    public async Task Port_CheckedExpression_notifies_the_cascaded_EditContext()
    {
        Preferences model = new();
        EditContext editContext = new(model);
        List<FieldIdentifier> changed = [];
        editContext.OnFieldChanged += (_, e) => changed.Add(e.FieldIdentifier);
        Expression<Func<bool>> expression = () => model.Notifications;
        IRenderedComponent<SwitchFieldHost> cut = Context.Render<SwitchFieldHost>(p => p
            .Add(x => x.EditContext, editContext)
            .Add(x => x.CheckedExpression, expression));

        Assert.Equal("Notifications", cut.Find("input[type=checkbox]").GetAttribute("name"));

        await test.ToggleAsync(cut, true);
        await test.SettleFormAsync(cut);
        await test.ToggleAsync(cut, false);
        await test.SettleFormAsync(cut);

        FieldIdentifier notifications = new(model, nameof(Preferences.Notifications));
        Assert.Equal([notifications, notifications], changed);
    }

    [Fact(DisplayName = "port: the EditContext's messages for the switch's identifier make its field invalid")]
    [Trait("Verifies", "ADR05-5")]
    public void Port_the_EditContexts_messages_make_the_field_invalid()
    {
        Preferences model = new();
        EditContext editContext = new(model);
        ValidationMessageStore store = new(editContext);
        Expression<Func<bool>> expression = () => model.Notifications;
        IRenderedComponent<SwitchFieldHost> cut = Context.Render<SwitchFieldHost>(p => p
            .Add(x => x.EditContext, editContext)
            .Add(x => x.CheckedExpression, expression)
            .Add(x => x.ShowError, true));
        Assert.False(cut.Find(Switch).HasAttribute("aria-invalid"));

        cut.InvokeAsync(() =>
        {
            store.Add(new FieldIdentifier(model, nameof(Preferences.Notifications)), "Turn notifications on.");
            editContext.NotifyValidationStateChanged();
        });

        Assert.Equal("true", cut.Find(Switch).GetAttribute("aria-invalid"));
        Assert.Equal("Turn notifications on.", cut.Find("[data-testid=error]").TextContent);
    }

    [Fact(DisplayName = "port: CheckedExpression without an EditContext does nothing")]
    [Trait("Verifies", "ADR05-5")]
    public async Task Port_CheckedExpression_without_an_EditContext_does_nothing()
    {
        Preferences model = new();
        Expression<Func<bool>> expression = () => model.Notifications;
        IRenderedComponent<SwitchFieldHost> cut = Context.Render<SwitchFieldHost>(p => p.Add(x => x.CheckedExpression, expression));

        await test.ToggleAsync(cut, true);
        await test.SettleFormAsync(cut);

        Assert.False(cut.Find("input[type=checkbox]").HasAttribute("name"));
        Assert.Equal("true", cut.Find(Switch).GetAttribute("aria-checked"));
    }

    // A native submission of the form element: the module prevents it and reports it with the state it reads, in the
    // submit task, from every element enrolled on the form: the root and the hidden input, each by its DOM id (an input
    // that renders no id under nativeButton is reported with an empty one, which nothing matches); C# answers.
    private async Task<string> SubmitAsync(IRenderedComponent<SwitchFieldHost> cut)
    {
        FormTwin form = ((DotNetObjectReference<FormTwin>)test.FormModule.Invocations["attach"]
            .Last(i => JsonSerializer.Deserialize((string)i.Arguments[1]!, BlazixJsonContext.Default.FormOptions)!.Submit is not null)
            .Arguments[2]!).Value;
        FormSubmitRequest request = new()
        {
            Event = new EventSnapshot { Kind = EventKind.Event, Type = "submit", DefaultPrevented = true, Target = new EventTargetInfo { TagName = "FORM" } },
            Controls =
            [
                new FormControlState { Id = cut.Find(Switch).GetAttribute("id") ?? string.Empty },
                new FormControlState { Id = cut.Find("input[type=checkbox]").GetAttribute("id") ?? string.Empty, Validity = test.InputValidity },
            ],
        };
        string json = JsonSerializer.Serialize(request, BlazixJsonContext.Default.FormSubmitRequest);
        string answer = await cut.InvokeAsync(() => form.OnSubmitRequest(json));
        await cut.InvokeAsync(static () => { });
        return answer;
    }

    private sealed class Preferences
    {
        public bool Notifications { get; set; }
    }
}
