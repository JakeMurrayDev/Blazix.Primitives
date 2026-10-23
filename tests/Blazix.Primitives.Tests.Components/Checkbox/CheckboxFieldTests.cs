using System.Linq.Expressions;
using AngleSharp.Dom;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// CheckboxRoot.test.tsx's "Field" block and the Field-rendering titles of its "id" and label blocks (issue 81), on a
// standalone box. A toggle reaches C# as the button module's report; the field's native validity is the hidden input's,
// read through the form module's refresh (SettleFormAsync); a blur is the form module's report on the root. Rows whose
// cited tests also render Form or Field.Error, and the group rows, are elsewhere or open (the PR lists them by ID).
/// <summary>A standalone checkbox inside a field: registration, the field's flags, validation, labels and descriptions.</summary>
public sealed class CheckboxFieldTests : IDisposable
{
    private const string Box = "[data-testid=box]";

    private readonly CheckboxTestContext test = new();

    private BunitContext Context => test.Context;

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "should receive disabled prop from Field.Root")]
    [Trait("Requirement", "CHK-API-11")]
    public void Should_receive_disabled_prop_from_Field_Root()
    {
        IRenderedComponent<CheckboxFieldHost> cut = Context.Render<CheckboxFieldHost>(p => p.Add(x => x.FieldDisabled, true));

        Assert.Equal("true", cut.Find(Box).GetAttribute("aria-disabled"));
        Assert.True(cut.Find("input[type=checkbox]").HasAttribute("disabled"));
    }

    [Fact(DisplayName = "should receive name prop from Field.Root")]
    [Trait("Requirement", "CHK-API-08")]
    public void Should_receive_name_prop_from_Field_Root()
    {
        IRenderedComponent<CheckboxFieldHost> cut = Context.Render<CheckboxFieldHost>(p => p.Add(x => x.FieldName, "field-checkbox").Add(x => x.BoxName, "own"));

        Assert.Equal("field-checkbox", cut.Find("input[type=checkbox]").GetAttribute("name"));
    }

    [Fact(DisplayName = "[data-touched]")]
    [Trait("Requirement", "CHK-FRM-14")]
    [Trait("Requirement", "CHK-DOM-05")]
    public async Task Data_touched()
    {
        IRenderedComponent<CheckboxFieldHost> cut = Context.Render<CheckboxFieldHost>();
        Assert.False(cut.Find(Box).HasAttribute("data-touched"));

        await cut.Find(Box).FocusAsync(new FocusEventArgs());
        await test.BlurAsync(cut);

        Assert.Equal(string.Empty, cut.Find(Box).GetAttribute("data-touched"));
    }

    [Fact(DisplayName = "[data-dirty]")]
    [Trait("Requirement", "CHK-STA-11")]
    [Trait("Requirement", "CHK-FRM-07")]
    [Trait("Requirement", "CHK-DOM-05")]
    public async Task Data_dirty()
    {
        IRenderedComponent<CheckboxFieldHost> cut = Context.Render<CheckboxFieldHost>();
        Assert.False(cut.Find(Box).HasAttribute("data-dirty"));

        await test.ToggleAsync(cut, true);
        await test.SettleFormAsync(cut);
        Assert.Equal(string.Empty, cut.Find(Box).GetAttribute("data-dirty"));

        // Port: back to the baseline captured at the first registration clears it.
        await test.ToggleAsync(cut, false);
        await test.SettleFormAsync(cut);
        Assert.False(cut.Find(Box).HasAttribute("data-dirty"));
    }

    [Fact(DisplayName = "adds [data-filled] attribute when checked after being initially unchecked")]
    [Trait("Requirement", "CHK-STA-10")]
    public async Task Adds_data_filled_attribute_when_checked_after_being_initially_unchecked()
    {
        IRenderedComponent<CheckboxFieldHost> cut = Context.Render<CheckboxFieldHost>();
        Assert.False(cut.Find(Box).HasAttribute("data-filled"));

        await test.ToggleAsync(cut, true);
        await test.SettleFormAsync(cut);
        Assert.Equal(string.Empty, cut.Find(Box).GetAttribute("data-filled"));

        await test.ToggleAsync(cut, false);
        await test.SettleFormAsync(cut);
        Assert.False(cut.Find(Box).HasAttribute("data-filled"));
    }

    [Fact(DisplayName = "removes [data-filled] attribute when unchecked after being initially checked")]
    [Trait("Requirement", "CHK-STA-10")]
    public async Task Removes_data_filled_attribute_when_unchecked_after_being_initially_checked()
    {
        IRenderedComponent<CheckboxFieldHost> cut = Context.Render<CheckboxFieldHost>(p => p.Add(x => x.DefaultChecked, true));
        Assert.Equal(string.Empty, cut.Find(Box).GetAttribute("data-filled"));

        await test.ToggleAsync(cut, false);
        await test.SettleFormAsync(cut);

        Assert.False(cut.Find(Box).HasAttribute("data-filled"));
    }

    [Fact(DisplayName = "clears [data-filled] when a controlled checkbox remounts unchecked")]
    [Trait("Requirement", "CHK-STA-10")]
    public void Clears_data_filled_when_a_controlled_checkbox_remounts_unchecked()
    {
        IRenderedComponent<CheckboxFieldHost> cut = Context.Render<CheckboxFieldHost>(p => p
            .Add(x => x.Controlled, true)
            .Add(x => x.Value, true)
            .Add(x => x.BoxKey, "true"));
        Assert.Equal(string.Empty, cut.Find("[data-testid=root]").GetAttribute("data-filled"));

        cut.Render(p => p.Add(x => x.Value, false).Add(x => x.BoxKey, "false"));

        Assert.False(cut.Find("[data-testid=root]").HasAttribute("data-filled"));
    }

    [Fact(DisplayName = "[data-focused]")]
    [Trait("Requirement", "CHK-FOC-04")]
    [Trait("Requirement", "CHK-DOM-05")]
    public async Task Data_focused()
    {
        IRenderedComponent<CheckboxFieldHost> cut = Context.Render<CheckboxFieldHost>();
        Assert.False(cut.Find(Box).HasAttribute("data-focused"));

        await cut.Find(Box).FocusAsync(new FocusEventArgs());
        Assert.Equal(string.Empty, cut.Find(Box).GetAttribute("data-focused"));

        await test.BlurAsync(cut);
        Assert.False(cut.Find(Box).HasAttribute("data-focused"));
    }

    [Fact(DisplayName = "does not set [data-focused] when disabled")]
    [Trait("Requirement", "CHK-FOC-04")]
    [Trait("Requirement", "CHK-FOC-02")]
    public async Task Does_not_set_data_focused_when_disabled()
    {
        IRenderedComponent<CheckboxFieldHost> cut = Context.Render<CheckboxFieldHost>(p => p.Add(x => x.BoxDisabled, true));

        await cut.Find(Box).FocusAsync(new FocusEventArgs());

        Assert.False(cut.Find(Box).HasAttribute("data-focused"));
    }

    [Fact(DisplayName = "is removed when the focused checkbox becomes disabled")]
    [Trait("Requirement", "CHK-FOC-05")]
    public async Task Is_removed_when_the_focused_checkbox_becomes_disabled()
    {
        IRenderedComponent<CheckboxFieldHost> cut = Context.Render<CheckboxFieldHost>();
        await cut.Find(Box).FocusAsync(new FocusEventArgs());
        Assert.Equal(string.Empty, cut.Find("[data-testid=root]").GetAttribute("data-focused"));

        cut.Render(p => p.Add(x => x.BoxDisabled, true));

        Assert.False(cut.Find("[data-testid=root]").HasAttribute("data-focused"));
        Assert.False(cut.Find(Box).HasAttribute("data-focused"));
    }

    [Fact(DisplayName = "is removed when the focused checkbox unmounts")]
    [Trait("Requirement", "CHK-FOC-05")]
    public async Task Is_removed_when_the_focused_checkbox_unmounts()
    {
        IRenderedComponent<CheckboxFieldHost> cut = Context.Render<CheckboxFieldHost>();
        await cut.Find(Box).FocusAsync(new FocusEventArgs());

        cut.Render(p => p.Add(x => x.BoxMounted, false));

        Assert.False(cut.Find("[data-testid=root]").HasAttribute("data-focused"));
    }

    [Fact(DisplayName = "port: a root that is already focused when its tracking starts acquires the focused state")]
    [Trait("Requirement", "CHK-FOC-05")]
    public async Task Port_a_root_already_focused_when_its_tracking_starts_acquires_the_focused_state()
    {
        // The StrictMode title "is kept when the checkbox is focused during mount in StrictMode", restated under ADR 06
        // Amendments 2026-10-18: the module reports a root that is the active element when it attaches.
        IRenderedComponent<CheckboxFieldHost> cut = Context.Render<CheckboxFieldHost>();

        await test.FocusedAtAttachAsync(cut);

        Assert.Equal(string.Empty, cut.Find("[data-testid=root]").GetAttribute("data-focused"));
    }

    [Fact(DisplayName = "[data-invalid]")]
    [Trait("Requirement", "CHK-DOM-06")]
    public void Data_invalid()
    {
        IRenderedComponent<CheckboxFieldHost> cut = Context.Render<CheckboxFieldHost>(p => p.Add(x => x.FieldInvalid, true));

        Assert.Equal(string.Empty, cut.Find(Box).GetAttribute("data-invalid"));
    }

    [Fact(DisplayName = "[data-valid]")]
    [Trait("Requirement", "CHK-DOM-06")]
    [Trait("Requirement", "CHK-FRM-11")]
    public async Task Data_valid()
    {
        IRenderedComponent<CheckboxFieldHost> cut = Context.Render<CheckboxFieldHost>(p => p.Add(x => x.ValidationMode, ValidationMode.OnBlur).Add(x => x.Required, true));
        Assert.False(cut.Find(Box).HasAttribute("data-valid"));
        Assert.False(cut.Find(Box).HasAttribute("data-invalid"));

        await test.ToggleAsync(cut, true);
        await test.SettleFormAsync(cut);
        await cut.Find(Box).FocusAsync(new FocusEventArgs());
        await test.BlurAsync(cut);

        Assert.Equal(string.Empty, cut.Find(Box).GetAttribute("data-valid"));
        Assert.False(cut.Find(Box).HasAttribute("data-invalid"));
    }

    [Fact(DisplayName = "props: validationMode=onChange")]
    [Trait("Requirement", "CHK-FRM-09")]
    [Trait("Requirement", "CHK-ARIA-11")]
    public async Task Props_validationMode_onChange()
    {
        IRenderedComponent<CheckboxFieldHost> cut = Context.Render<CheckboxFieldHost>(p => p
            .Add(x => x.ValidationMode, ValidationMode.OnChange)
            .Add(x => x.Validate, args => ValueTask.FromResult<FieldValidationResult?>((bool)args.Value! ? "error" : null)));
        Assert.False(cut.Find(Box).HasAttribute("aria-invalid"));

        await test.ToggleAsync(cut, true);
        await test.SettleFormAsync(cut);

        Assert.Equal("true", cut.Find(Box).GetAttribute("aria-invalid"));
    }

    [Fact(DisplayName = "validates once when changed by the user")]
    [Trait("Requirement", "CHK-FRM-09")]
    [Trait("Requirement", "CHK-FRM-06")]
    public async Task Validates_once_when_changed_by_the_user()
    {
        List<object?> values = [];
        IRenderedComponent<CheckboxFieldHost> cut = Context.Render<CheckboxFieldHost>(p => p
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
    [Trait("Requirement", "CHK-FRM-10")]
    public async Task Revalidates_when_a_controlled_value_changes_externally()
    {
        List<object?> values = [];
        IRenderedComponent<CheckboxFieldHost> cut = Context.Render<CheckboxFieldHost>(p => p
            .Add(x => x.FieldName, "terms")
            .Add(x => x.ValidationMode, ValidationMode.OnChange)
            .Add(x => x.Validate, args =>
            {
                values.Add(args.Value);
                return ValueTask.FromResult<FieldValidationResult?>((bool)args.Value! ? "error" : null);
            })
            .Add(x => x.Controlled, true)
            .Add(x => x.Value, false));
        Assert.False(cut.Find(Box).HasAttribute("aria-invalid"));
        int initial = values.Count;

        cut.Render(p => p.Add(x => x.Value, true));
        await test.SettleFormAsync(cut);

        Assert.Equal(initial + 1, values.Count);
        Assert.Equal(true, values[^1]);
        Assert.Equal("true", cut.Find(Box).GetAttribute("aria-invalid"));
    }

    [Fact(DisplayName = "prop: validationMode=onBlur")]
    [Trait("Requirement", "CHK-FRM-11")]
    public async Task Prop_validationMode_onBlur()
    {
        IRenderedComponent<CheckboxFieldHost> cut = Context.Render<CheckboxFieldHost>(p => p
            .Add(x => x.ValidationMode, ValidationMode.OnBlur)
            .Add(x => x.Validate, args => ValueTask.FromResult<FieldValidationResult?>((bool)args.Value! ? "error" : null)));
        Assert.False(cut.Find(Box).HasAttribute("aria-invalid"));

        await test.ToggleAsync(cut, true);
        await test.SettleFormAsync(cut);
        Assert.False(cut.Find(Box).HasAttribute("aria-invalid"));

        await test.BlurAsync(cut);

        Assert.Equal("true", cut.Find(Box).GetAttribute("aria-invalid"));
    }

    [Fact(DisplayName = "port: the root and the hidden input carry aria-invalid while the field is invalid and enabled")]
    [Trait("Requirement", "CHK-ARIA-11")]
    [Trait("Requirement", "CHK-DOM-18")]
    public void Port_the_root_and_the_hidden_input_carry_aria_invalid()
    {
        IRenderedComponent<CheckboxFieldHost> cut = Context.Render<CheckboxFieldHost>(p => p.Add(x => x.FieldInvalid, true));
        Assert.Equal("true", cut.Find(Box).GetAttribute("aria-invalid"));
        Assert.Equal("true", cut.Find("input[type=checkbox]").GetAttribute("aria-invalid"));

        cut.Render(p => p.Add(x => x.BoxDisabled, true));
        Assert.False(cut.Find(Box).HasAttribute("aria-invalid"));
        Assert.False(cut.Find("input[type=checkbox]").HasAttribute("aria-invalid"));
    }

    [Fact(DisplayName = "when label and checkbox are siblings")]
    [Trait("Requirement", "CHK-FRM-16")]
    [Trait("Requirement", "CHK-ARIA-06")]
    public async Task When_label_and_checkbox_are_siblings()
    {
        IRenderedComponent<CheckboxFieldHost> cut = Context.Render<CheckboxFieldHost>(p => p.Add(x => x.ShowLabel, true));

        IElement label = cut.Find("[data-testid=label]");
        Assert.NotNull(label.GetAttribute("id"));
        Assert.Equal(cut.Find("input[type=checkbox]").GetAttribute("id"), label.GetAttribute("for"));
        Assert.Equal(label.GetAttribute("id"), cut.Find(Box).GetAttribute("aria-labelledby"));
        Assert.Equal("false", cut.Find(Box).GetAttribute("aria-checked"));

        // The label's click is native: the browser clicks the input it points at (tests/browser/checkbox.spec.ts).
        await test.ToggleAsync(cut, true);
        Assert.Equal("true", cut.Find(Box).GetAttribute("aria-checked"));
    }

    [Fact(DisplayName = "sets `for` on the label")]
    [Trait("Requirement", "CHK-FRM-16")]
    [Trait("Requirement", "CHK-ARIA-06")]
    [Trait("Requirement", "CHK-DOM-15")]
    public void Sets_for_on_the_label()
    {
        IRenderedComponent<CheckboxFieldHost> cut = Context.Render<CheckboxFieldHost>(p => p.Add(x => x.ShowLabel, true).Add(x => x.LabelWraps, true));

        IElement label = cut.Find("[data-testid=label]");
        string? @for = label.GetAttribute("for");
        Assert.NotNull(@for);
        Assert.Equal(cut.Find("input[type=checkbox]").GetAttribute("id"), @for);
        Assert.NotNull(label.GetAttribute("id"));
        Assert.Equal(label.GetAttribute("id"), cut.Find(Box).GetAttribute("aria-labelledby"));
    }

    [Fact(DisplayName = "Field.Description")]
    [Trait("Requirement", "CHK-ARIA-10")]
    [Trait("Requirement", "CHK-FRM-17")]
    [Trait("Requirement", "CHK-DOM-18")]
    public void Field_Description()
    {
        IRenderedComponent<CheckboxFieldHost> cut = Context.Render<CheckboxFieldHost>(p => p.Add(x => x.ShowDescription, true).Add(x => x.AriaDescribedBy, "external-description"));

        string descriptionId = cut.Find("[data-testid=description]").GetAttribute("id")!;
        Assert.Equal($"external-description {descriptionId}", cut.Find(Box).GetAttribute("aria-describedby"));

        // CHK-DOM-18: the hidden input gets the field's ids without the consumer's tokens.
        Assert.Equal(descriptionId, cut.Find("input[type=checkbox]").GetAttribute("aria-describedby"));
    }

    [Fact(DisplayName = "falls back to the Field control id when id is empty")]
    [Trait("Requirement", "CHK-API-14")]
    [Trait("Requirement", "CHK-DOM-15")]
    public void Falls_back_to_the_Field_control_id_when_id_is_empty()
    {
        IRenderedComponent<CheckboxFieldHost> cut = Context.Render<CheckboxFieldHost>(p => p.Add(x => x.ShowLabel, true).Add(x => x.BoxId, string.Empty));

        string? inputId = cut.Find("input[type=checkbox]").GetAttribute("id");
        Assert.False(string.IsNullOrEmpty(inputId));
        Assert.Equal(inputId, cut.Find("[data-testid=label]").GetAttribute("for"));
    }

    [Theory(DisplayName = "drops an explicit id when the prop is removed (nativeButton=%s)")]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Requirement", "CHK-FRM-16")]
    [Trait("Requirement", "CHK-DOM-21")]
    [Trait("Requirement", "CHK-DOM-01")]
    public void Drops_an_explicit_id_when_the_prop_is_removed(bool nativeButton)
    {
        IRenderedComponent<CheckboxFieldHost> cut = Context.Render<CheckboxFieldHost>(p => p.Add(x => x.ShowLabel, true).Add(x => x.NativeButton, nativeButton).Add(x => x.BoxId, "explicit"));
        Assert.Equal("explicit", LabelControl(cut, nativeButton).GetAttribute("id"));
        Assert.Equal("explicit", cut.Find("[data-testid=label]").GetAttribute("for"));

        cut.Render(p => p.Add(x => x.BoxId, null));

        string? id = LabelControl(cut, nativeButton).GetAttribute("id");
        Assert.False(string.IsNullOrEmpty(id));
        Assert.NotEqual("explicit", id);
        Assert.Equal(id, cut.Find("[data-testid=label]").GetAttribute("for"));
    }

    [Theory(DisplayName = "does not reuse an unmounted Checkbox id for a keyed id-less Checkbox (nativeButton=%s)")]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Requirement", "CHK-FRM-16")]
    [Trait("Requirement", "CHK-DOM-21")]
    public void Does_not_reuse_an_unmounted_Checkbox_id_for_a_keyed_id_less_Checkbox(bool nativeButton)
    {
        IRenderedComponent<CheckboxFieldHost> cut = Context.Render<CheckboxFieldHost>(p => p
            .Add(x => x.ShowLabel, true)
            .Add(x => x.NativeButton, nativeButton)
            .Add(x => x.BoxKey, "explicit")
            .Add(x => x.BoxId, "explicit"));
        Assert.Equal("explicit", cut.Find("[data-testid=label]").GetAttribute("for"));

        cut.Render(p => p.Add(x => x.BoxKey, "generated").Add(x => x.BoxId, null));

        string? id = LabelControl(cut, nativeButton).GetAttribute("id");
        Assert.False(string.IsNullOrEmpty(id));
        Assert.NotEqual("explicit", id);
        Assert.Equal(id, cut.Find("[data-testid=label]").GetAttribute("for"));
    }

    [Fact(DisplayName = "prefers `aria-label` over an associated label")]
    [Trait("Requirement", "CHK-ARIA-06")]
    public void Prefers_aria_label_over_an_associated_label()
    {
        // The Field.Label half; the native label half is tests/browser/checkbox.spec.ts.
        IRenderedComponent<CheckboxFieldHost> cut = Context.Render<CheckboxFieldHost>(p => p.Add(x => x.ShowLabel, true).Add(x => x.LabelWraps, true).Add(x => x.AriaLabel, "notes.txt"));

        Assert.False(cut.Find(Box).HasAttribute("aria-labelledby"));
        Assert.Equal("notes.txt", cut.Find(Box).GetAttribute("aria-label"));
    }

    [Theory(DisplayName = "ignores a blank `aria-label` (%j)")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\n\t ")]
    [Trait("Requirement", "CHK-EDGE-07")]
    [Trait("Requirement", "CHK-ARIA-06")]
    public void Ignores_a_blank_aria_label(string ariaLabel)
    {
        IRenderedComponent<CheckboxFieldHost> cut = Context.Render<CheckboxFieldHost>(p => p.Add(x => x.ShowLabel, true).Add(x => x.LabelWraps, true).Add(x => x.AriaLabel, ariaLabel));

        Assert.Equal(cut.Find("[data-testid=label]").GetAttribute("id"), cut.Find(Box).GetAttribute("aria-labelledby"));
        Assert.Equal(ariaLabel, cut.Find(Box).GetAttribute("aria-label"));
    }

    [Fact(DisplayName = "updates the accessible name when `aria-label` is added, cleared, or removed")]
    [Trait("Requirement", "CHK-EDGE-13")]
    public void Updates_the_accessible_name_when_aria_label_is_added_cleared_or_removed()
    {
        // `dynamic Field.Label label`; the native variant is tests/browser/checkbox.spec.ts.
        IRenderedComponent<CheckboxFieldHost> cut = Context.Render<CheckboxFieldHost>(p => p.Add(x => x.ShowLabel, true).Add(x => x.BoxId, "dynamic-label"));
        string labelId = cut.Find("[data-testid=label]").GetAttribute("id")!;
        Assert.Equal(labelId, cut.Find(Box).GetAttribute("aria-labelledby"));

        cut.Render(p => p.Add(x => x.AriaLabel, "Custom name"));
        Assert.False(cut.Find(Box).HasAttribute("aria-labelledby"));

        cut.Render(p => p.Add(x => x.AriaLabel, " \n\t "));
        Assert.Equal(labelId, cut.Find(Box).GetAttribute("aria-labelledby"));

        cut.Render(p => p.Add(x => x.AriaLabel, "Custom name"));
        Assert.False(cut.Find(Box).HasAttribute("aria-labelledby"));

        cut.Render(p => p.Add(x => x.AriaLabel, null));
        Assert.Equal(labelId, cut.Find(Box).GetAttribute("aria-labelledby"));
    }

    [Fact(DisplayName = "updates the accessible name when the associated label mounts and unmounts")]
    [Trait("Requirement", "CHK-EDGE-13")]
    public void Updates_the_accessible_name_when_the_associated_label_mounts_and_unmounts()
    {
        IRenderedComponent<CheckboxFieldHost> cut = Context.Render<CheckboxFieldHost>(p => p.Add(x => x.ShowLabel, false).Add(x => x.BoxId, "dynamic-label"));
        Assert.False(cut.Find(Box).HasAttribute("aria-labelledby"));

        cut.Render(p => p.Add(x => x.ShowLabel, true));
        Assert.Equal(cut.Find("[data-testid=label]").GetAttribute("id"), cut.Find(Box).GetAttribute("aria-labelledby"));

        cut.Render(p => p.Add(x => x.ShowLabel, false));
        Assert.False(cut.Find(Box).HasAttribute("aria-labelledby"));

        cut.Render(p => p.Add(x => x.AriaLabel, "Custom name"));
        cut.Render(p => p.Add(x => x.ShowLabel, true));
        Assert.False(cut.Find(Box).HasAttribute("aria-labelledby"));

        cut.Render(p => p.Add(x => x.ShowLabel, true).Add(x => x.AriaLabel, null));
        Assert.Equal(cut.Find("[data-testid=label]").GetAttribute("id"), cut.Find(Box).GetAttribute("aria-labelledby"));
    }

    [Fact(DisplayName = "port: the box registers as the field's control with its checked value, and not while disabled")]
    [Trait("Requirement", "CHK-FRM-06")]
    [Trait("Requirement", "CHK-FRM-19")]
    public async Task Port_the_box_registers_as_the_fields_control()
    {
        List<object?> values = [];
        IRenderedComponent<CheckboxFieldHost> cut = Context.Render<CheckboxFieldHost>(p => p
            .Add(x => x.BoxName, "own-name")
            .Add(x => x.ValidationMode, ValidationMode.OnChange)
            .Add(x => x.Validate, args =>
            {
                values.Add(args.Value);
                return ValueTask.FromResult<FieldValidationResult?>(null);
            }));

        await test.ToggleAsync(cut, true);
        await test.SettleFormAsync(cut);
        Assert.Equal([true], values);

        // CHK-FRM-19: the hidden input is the field's validation input, attached with the root as its focus target.
        FormControlOptions input = test.FormOptions.Last(o => !o.ReportEvents);
        Assert.Equal(cut.Find(Box).GetAttribute("id"), input.FocusTarget);

        cut.Render(p => p.Add(x => x.BoxDisabled, true));
        await test.ToggleAsync(cut, false);
        Assert.Equal([true], values);
    }

    [Fact(DisplayName = "port: a box inside a Field.Item claims the item's label and descriptions and is disabled with it")]
    [Trait("Requirement", "CHK-FRM-21")]
    [Trait("Requirement", "CHK-ARIA-10")]
    public void Port_a_box_inside_a_Field_Item_claims_the_items_label_and_descriptions()
    {
        IRenderedComponent<CheckboxFieldHost> cut = Context.Render<CheckboxFieldHost>(p => p.Add(x => x.InItem, true).Add(x => x.ShowDescription, true));

        IElement itemLabel = cut.Find("[data-testid=item-label]");
        Assert.Equal(cut.Find("input[type=checkbox]").GetAttribute("id"), itemLabel.GetAttribute("for"));
        Assert.Equal(itemLabel.GetAttribute("id"), cut.Find(Box).GetAttribute("aria-labelledby"));
        string rootDescription = cut.Find("[data-testid=description]").GetAttribute("id")!;
        string itemDescription = cut.Find("[data-testid=item-description]").GetAttribute("id")!;
        Assert.Equal($"{rootDescription} {itemDescription}", cut.Find(Box).GetAttribute("aria-describedby"));

        cut.Render(p => p.Add(x => x.ItemDisabled, true));
        Assert.Equal("true", cut.Find(Box).GetAttribute("aria-disabled"));
    }

    [Fact(DisplayName = "port: CheckedExpression notifies the cascaded EditContext of every committed change and names the field")]
    [Trait("Verifies", "ADR05-5")]
    public async Task Port_CheckedExpression_notifies_the_cascaded_EditContext()
    {
        Consent model = new();
        EditContext editContext = new(model);
        List<FieldIdentifier> changed = [];
        editContext.OnFieldChanged += (_, e) => changed.Add(e.FieldIdentifier);
        Expression<Func<bool>> expression = () => model.Accepted;
        IRenderedComponent<CheckboxFieldHost> cut = Context.Render<CheckboxFieldHost>(p => p
            .Add(x => x.EditContext, editContext)
            .Add(x => x.CheckedExpression, expression));

        Assert.Equal("Accepted", cut.Find("input[type=checkbox]").GetAttribute("name"));

        await test.ToggleAsync(cut, true);
        await test.SettleFormAsync(cut);
        await test.ToggleAsync(cut, false);
        await test.SettleFormAsync(cut);

        FieldIdentifier accepted = new(model, nameof(Consent.Accepted));
        Assert.Equal([accepted, accepted], changed);
    }

    [Fact(DisplayName = "port: the EditContext's messages for the box's identifier make its field invalid")]
    [Trait("Verifies", "ADR05-5")]
    public void Port_the_EditContexts_messages_make_the_field_invalid()
    {
        Consent model = new();
        EditContext editContext = new(model);
        ValidationMessageStore store = new(editContext);
        Expression<Func<bool>> expression = () => model.Accepted;
        IRenderedComponent<CheckboxFieldHost> cut = Context.Render<CheckboxFieldHost>(p => p
            .Add(x => x.EditContext, editContext)
            .Add(x => x.CheckedExpression, expression));
        Assert.False(cut.Find(Box).HasAttribute("aria-invalid"));

        cut.InvokeAsync(() =>
        {
            store.Add(new FieldIdentifier(model, nameof(Consent.Accepted)), "Accept the terms.");
            editContext.NotifyValidationStateChanged();
        });

        Assert.Equal("true", cut.Find(Box).GetAttribute("aria-invalid"));
    }

    [Fact(DisplayName = "port: CheckedExpression without an EditContext does nothing")]
    [Trait("Verifies", "ADR05-5")]
    public async Task Port_CheckedExpression_without_an_EditContext_does_nothing()
    {
        Consent model = new();
        Expression<Func<bool>> expression = () => model.Accepted;
        IRenderedComponent<CheckboxFieldHost> cut = Context.Render<CheckboxFieldHost>(p => p.Add(x => x.CheckedExpression, expression));

        await test.ToggleAsync(cut, true);
        await test.SettleFormAsync(cut);

        Assert.False(cut.Find("input[type=checkbox]").HasAttribute("name"));
        Assert.Equal("true", cut.Find(Box).GetAttribute("aria-checked"));
    }

    private static IElement LabelControl(IRenderedComponent<CheckboxFieldHost> cut, bool nativeButton) =>
        nativeButton ? cut.Find(Box) : cut.Find("input[type=checkbox]");

    private sealed class Consent
    {
        public bool Accepted { get; set; }
    }
}
