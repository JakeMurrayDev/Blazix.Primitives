using AngleSharp.Dom;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using static Blazix.Primitives.Tests.Components.RadioGroupFieldHost;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// RadioGroup.test.tsx's Field, Field.Label, Field.Description, validation-mode and Form titles at L2 (issue 84), with the
// Radio-owned rows of radio.md §13. The group is its field's control (RDG §8); each radio's hidden input is one of the
// field's validation inputs, read through the form module's refresh (SettleAllFormsAsync answers them) and at submit
// (SubmitAsync reports the state the module read). FormData and the browser's focus are L4 (radio-group.spec.ts).
/// <summary>A radio group inside a field and a form: registration, the field's flags, validation, labels and ids.</summary>
public sealed class RadioGroupFieldTests : IDisposable
{
    private static readonly NativeValidity s_missing = NativeValidity.Default with { Valid = false, ValueMissing = true, ValidationMessage = "Select one of these options." };

    private readonly RadioTestContext test = new();

    private BunitContext Context => test.Context;

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "passes the `name` prop to the radio input")]
    [Trait("Requirement", "RDG-API-07")]
    [Trait("Requirement", "FFI-STA-10")]
    public void Passes_the_name_prop_to_the_radio_input()
    {
        IRenderedComponent<RadioGroupFieldHost> cut = Context.Render<RadioGroupFieldHost>(p => p
            .Add(x => x.FieldName, "test")
            .Add(x => x.GroupName, "group")
            .Add(x => x.Items, ItemLayout.SiblingLabel)
            .Add(x => x.Radios, [new HostRadio("item", "a")]));

        Assert.Equal("test", cut.Find("[data-testid=item]").NextElementSibling!.GetAttribute("name"));
    }

    [Fact(DisplayName = "should receive disabled prop from Field.Root")]
    [Trait("Requirement", "RDG-API-04")]
    [Trait("Requirement", "RDG-ARIA-03")]
    [Trait("Requirement", "RDG-DOM-02")]
    [Trait("Requirement", "RAD-API-02")]
    public void Should_receive_disabled_prop_from_Field_Root()
    {
        IRenderedComponent<RadioGroupFieldHost> cut = Context.Render<RadioGroupFieldHost>(p => p
            .Add(x => x.FieldDisabled, true)
            .Add(x => x.Items, ItemLayout.SiblingLabel)
            .Add(x => x.Radios, [new HostRadio("radio", "a")]));

        IElement group = cut.Find("[role=radiogroup]");
        IElement radio = cut.Find("[data-testid=radio]");
        Assert.Equal("true", group.GetAttribute("aria-disabled"));
        Assert.True(group.HasAttribute("data-disabled"));
        Assert.Equal("true", radio.GetAttribute("aria-disabled"));
        Assert.True(radio.HasAttribute("data-disabled"));
    }

    [Fact(DisplayName = "should receive name prop from Field.Root")]
    [Trait("Requirement", "RDG-API-07")]
    public void Should_receive_name_prop_from_Field_Root()
    {
        IRenderedComponent<RadioGroupFieldHost> cut = Context.Render<RadioGroupFieldHost>(p => p
            .Add(x => x.FieldName, "field-radio")
            .Add(x => x.Controlled, true)
            .Add(x => x.Value, "a")
            .Add(x => x.Items, ItemLayout.SiblingLabel)
            .Add(x => x.Radios, [new HostRadio("radio", "a")]));

        Assert.Equal("field-radio", cut.Find("[data-testid=radio]").NextElementSibling!.GetAttribute("name"));
    }

    [Fact(DisplayName = "is not acquired when a radio inherits disabled from Field.Item")]
    [Trait("Requirement", "RAD-API-02")]
    [Trait("Requirement", "RAD-EDGE-11")]
    [Trait("Requirement", "RAD-FOC-03")]
    public async Task Is_not_acquired_when_a_radio_inherits_disabled_from_Field_Item()
    {
        IRenderedComponent<RadioGroupFieldHost> cut = Context.Render<RadioGroupFieldHost>(p => p
            .Add(x => x.Items, ItemLayout.SiblingLabel)
            .Add(x => x.Radios, [new HostRadio("disabled-radio", "a") { ItemDisabled = true }, new HostRadio("b")]));

        IElement radio = cut.Find("[data-testid=disabled-radio]");
        Assert.Equal("true", radio.GetAttribute("aria-disabled"));
        await radio.TriggerEventAsync("onfocus", new FocusEventArgs());

        Assert.False(cut.Find("[data-testid=field]").HasAttribute("data-focused"));
    }

    [Fact(DisplayName = "revalidates when the controlled value changes externally")]
    [Trait("Requirement", "RDG-FRM-10")]
    [Trait("Requirement", "RDG-EVT-05")]
    [Trait("Requirement", "RDG-STA-03")]
    [Trait("Requirement", "RDG-API-01")]
    public async Task Revalidates_when_the_controlled_value_changes_externally()
    {
        List<object?> validated = [];
        IRenderedComponent<RadioGroupFieldHost> cut = Context.Render<RadioGroupFieldHost>(p => p
            .Add(x => x.FieldName, "choices")
            .Add(x => x.ValidationMode, ValidationMode.OnChange)
            .Add(x => x.Validate, args =>
            {
                validated.Add(args.Value);
                return ValueTask.FromResult<FieldValidationResult?>((string?)args.Value == "b" ? "error" : null);
            })
            .Add(x => x.Controlled, true)
            .Add(x => x.Value, "a")
            .Add(x => x.Items, ItemLayout.SiblingLabel)
            .Add(x => x.Radios, [new HostRadio("radio-a", "a"), new HostRadio("radio-b", "b")]));
        IElement Group() => cut.Find("[role=radiogroup]");
        Assert.False(Group().HasAttribute("aria-invalid"));
        int initial = validated.Count;

        cut.Render(p => p.Add(x => x.Value, "b"));
        await test.SettleAllFormsAsync(cut);

        Assert.Equal(initial + 1, validated.Count);
        Assert.Equal("b", validated[^1]);
        Assert.Equal("true", Group().GetAttribute("aria-invalid"));
        Assert.Empty(cut.Instance.Changes);
    }

    [Fact(DisplayName = "associates implicitly (Field.Label)")]
    [Trait("Requirement", "RDG-FRM-13")]
    [Trait("Requirement", "RAD-FRM-06")]
    [Trait("Requirement", "RAD-ARIA-03")]
    public void Associates_implicitly_Field_Label()
    {
        IRenderedComponent<RadioGroupFieldHost> cut = Context.Render<RadioGroupFieldHost>(p => p
            .Add(x => x.FieldName, "options")
            .Add(x => x.Items, ItemLayout.WrappingLabel)
            .Add(x => x.Radios, [new HostRadio("apple"), new HostRadio("banana")]));

        IReadOnlyList<IElement> labels = cut.FindAll("[data-testid=label]");
        IReadOnlyList<IElement> inputs = RadioTestContext.Inputs(cut);
        IReadOnlyList<IElement> radios = RadioTestContext.Radios(cut);
        Assert.Equal(2, labels.Count);
        for (int i = 0; i < 2; i++)
        {
            // The label's `for` names the radio's hidden input, whose activation the browser does (L4); the radio is named by it.
            Assert.Equal(inputs[i].Id, labels[i].GetAttribute("for"));
            Assert.Equal(labels[i].Id, radios[i].GetAttribute("aria-labelledby"));
        }
    }

    [Fact(DisplayName = "associates explicitly (Field.Label)")]
    [Trait("Requirement", "RDG-FRM-13")]
    [Trait("Requirement", "RDG-FRM-14")]
    [Trait("Requirement", "RAD-FRM-06")]
    [Trait("Requirement", "RAD-ARIA-07")]
    [Trait("Requirement", "RAD-DOM-10")]
    public void Associates_explicitly_Field_Label()
    {
        IRenderedComponent<RadioGroupFieldHost> cut = Context.Render<RadioGroupFieldHost>(p => p
            .Add(x => x.FieldName, "options")
            .Add(x => x.Items, ItemLayout.SiblingLabel)
            .Add(x => x.Radios, [new HostRadio("apple"), new HostRadio("banana")]));

        IReadOnlyList<IElement> radios = RadioTestContext.Radios(cut);
        IReadOnlyList<IElement> labels = cut.FindAll("[data-testid=label]");
        IReadOnlyList<IElement> descriptions = cut.FindAll("[data-testid=description]");
        IReadOnlyList<IElement> inputs = RadioTestContext.Inputs(cut);
        for (int i = 0; i < radios.Count; i++)
        {
            Assert.NotNull(labels[i].GetAttribute("for"));
            Assert.Equal(inputs[i].GetAttribute("id"), labels[i].GetAttribute("for"));
            Assert.NotNull(descriptions[i].GetAttribute("id"));
            Assert.Equal(descriptions[i].GetAttribute("id"), radios[i].GetAttribute("aria-describedby"));
        }
    }

    [Fact(DisplayName = "links the group and individual radios")]
    [Trait("Requirement", "RDG-ARIA-06")]
    [Trait("Requirement", "RDG-FRM-14")]
    [Trait("Requirement", "RAD-ARIA-07")]
    public void Links_the_group_and_individual_radios()
    {
        IRenderedComponent<RadioGroupFieldHost> cut = Context.Render<RadioGroupFieldHost>(p => p
            .Add(x => x.FieldName, "apple")
            .Add(x => x.ShowGroupDescription, true)
            .Add(x => x.GroupAttributes, new Dictionary<string, object> { ["aria-describedby"] = "external-description" })
            .Add(x => x.Items, ItemLayout.WrappingLabel)
            .Add(x => x.Radios, [new HostRadio("fuji-apple") { DescribedBy = "radio-description" }]));

        string? groupDescriptionId = cut.Find("[data-testid=group-description]").GetAttribute("id");
        Assert.NotNull(groupDescriptionId);
        Assert.Equal($"radio-description {groupDescriptionId}", cut.Find("[role=radio]").GetAttribute("aria-describedby"));
        Assert.Equal($"external-description {groupDescriptionId}", cut.Find("[role=radiogroup]").GetAttribute("aria-describedby"));
    }

    [Fact(DisplayName = "onSubmit")]
    [Trait("Requirement", "RDG-FRM-05")]
    [Trait("Requirement", "RDG-FRM-10")]
    [Trait("Requirement", "RDG-ARIA-07")]
    public async Task OnSubmit()
    {
        IRenderedComponent<RadioGroupFieldHost> cut = Context.Render<RadioGroupFieldHost>(p => p
            .Add(x => x.InForm, true)
            .Add(x => x.Validate, args => ValueTask.FromResult<FieldValidationResult?>((string?)args.Value switch
            {
                "a" => "custom error a",
                "c" => "custom error c",
                _ => null,
            }))
            .Add(x => x.Radios, [new HostRadio("a"), new HostRadio("b"), new HostRadio("c")]));
        IElement Group() => cut.Find("[role=radiogroup]");
        Assert.False(Group().HasAttribute("aria-invalid"));

        await SelectAsync(cut, 0);
        Assert.True(RadioTestContext.Radios(cut)[0].HasAttribute("data-checked"));
        Assert.False(Group().HasAttribute("aria-invalid"));

        await SelectAsync(cut, 2);
        Assert.True(RadioTestContext.Radios(cut)[2].HasAttribute("data-checked"));
        Assert.False(Group().HasAttribute("aria-invalid"));

        await test.SubmitAsync(cut);
        Assert.Equal("true", Group().GetAttribute("aria-invalid"));

        await SelectAsync(cut, 1);
        Assert.True(RadioTestContext.Radios(cut)[1].HasAttribute("data-checked"));
        Assert.False(Group().HasAttribute("aria-invalid"));
    }

    // RadioGroup.test.tsx "onBlur validates only when focus leaves the group" (RDG-FRM-11 is L4; this is the C# half): a blur
    // between radios reaches C# as nothing (design point R4, pending the user); focus leaving the group is one report.
    [Fact(DisplayName = "port: onBlur validates only on the group's leaving-blur report, once")]
    [Trait("Requirement", "RDG-FRM-11")]
    [Trait("Requirement", "RDG-FOC-05")]
    [Trait("Requirement", "RDG-FOC-04")]
    public async Task Port_onBlur_validates_only_on_the_groups_leaving_blur_report()
    {
        List<object?> validated = [];
        IRenderedComponent<RadioGroupFieldHost> cut = Context.Render<RadioGroupFieldHost>(p => p
            .Add(x => x.ValidationMode, ValidationMode.OnBlur)
            .Add(x => x.Validate, args =>
            {
                validated.Add(args.Value);
                return ValueTask.FromResult<FieldValidationResult?>((string?)args.Value == "a" ? "error" : null);
            })
            .Add(x => x.DefaultValue, "a")
            .Add(x => x.Radios, [new HostRadio("radio-a", "a"), new HostRadio("radio-b", "b")]));
        IRenderedComponent<RadioRoot<string?>>[] radios = [.. cut.FindComponents<RadioRoot<string?>>()];
        IElement Field() => cut.Find("[data-testid=field]");

        await RadioTestContext.Radios(cut)[0].TriggerEventAsync("onfocus", new FocusEventArgs());
        Assert.Equal(string.Empty, Field().GetAttribute("data-focused"));
        await test.BlurAsync(cut, radios[0].Instance.Element);
        await RadioTestContext.Radios(cut)[1].TriggerEventAsync("onfocus", new FocusEventArgs());

        Assert.Empty(validated);
        Assert.Equal(string.Empty, Field().GetAttribute("data-focused"));
        Assert.False(Field().HasAttribute("data-touched"));

        await test.FocusLeaveAsync(cut);

        Assert.Equal(["a"], validated);
        Assert.Equal("true", cut.Find("[role=radiogroup]").GetAttribute("aria-invalid"));
        Assert.False(Field().HasAttribute("data-focused"));
        Assert.Equal(string.Empty, Field().GetAttribute("data-touched"));
    }

    [Fact(DisplayName = "port: the leaving-blur report is requested only inside a field")]
    [Trait("Requirement", "RDG-FOC-05")]
    public void Port_the_leaving_blur_report_is_requested_only_inside_a_field()
    {
        IRenderedComponent<RadioGroupFieldHost> cut = Context.Render<RadioGroupFieldHost>(p => p.Add(x => x.Radios, [new HostRadio("a")]));

        Assert.Contains("\"reportFocusLeave\":true", test.LastCompositeOptions, StringComparison.Ordinal);
    }

    // RadioGroup.test.tsx "should automatically select radio upon navigation" (RDG-KEY-03 is L4): the C# half, a selection
    // that is not canceled touches the field, and only a change of the value fills it.
    [Fact(DisplayName = "port: a selection touches the field and fills it")]
    [Trait("Requirement", "RAD-FRM-05")]
    [Trait("Requirement", "RAD-EVT-01")]
    [Trait("Requirement", "RDG-STA-05")]
    [Trait("Requirement", "RDG-DOM-03")]
    public async Task Port_a_selection_touches_the_field_and_fills_it()
    {
        IRenderedComponent<RadioGroupFieldHost> cut = Context.Render<RadioGroupFieldHost>(p => p
            .Add(x => x.Radios, [new HostRadio("a"), new HostRadio("b")]));
        IElement Group() => cut.Find("[role=radiogroup]");
        Assert.False(Group().HasAttribute("data-touched"));
        Assert.False(Group().HasAttribute("data-filled"));

        await SelectAsync(cut, 1);

        Assert.Equal(string.Empty, Group().GetAttribute("data-touched"));
        Assert.Equal(string.Empty, Group().GetAttribute("data-filled"));
        Assert.Equal(string.Empty, Group().GetAttribute("data-dirty"));
        Assert.Equal(string.Empty, RadioTestContext.Radios(cut)[1].GetAttribute("data-touched"));
    }

    [Fact(DisplayName = "port: a radio mounted checked fills its field, and only the group clears it")]
    [Trait("Requirement", "RAD-STA-05")]
    [Trait("Requirement", "RDG-STA-05")]
    public void Port_a_radio_mounted_checked_fills_its_field()
    {
        IRenderedComponent<RadioGroupFieldHost> cut = Context.Render<RadioGroupFieldHost>(p => p
            .Add(x => x.DefaultValue, "b")
            .Add(x => x.Radios, [new HostRadio("a"), new HostRadio("b")]));

        Assert.Equal(string.Empty, cut.Find("[role=radiogroup]").GetAttribute("data-filled"));
        Assert.False(cut.Find("[role=radiogroup]").HasAttribute("data-dirty"));
    }

    [Fact(DisplayName = "submits null to onFormSubmit when no radio is selected")]
    [Trait("Requirement", "RDG-FRM-05")]
    [Trait("Requirement", "RDG-FRM-06")]
    public async Task Submits_null_to_onFormSubmit_when_no_radio_is_selected()
    {
        IRenderedComponent<RadioGroupFieldHost> cut = Context.Render<RadioGroupFieldHost>(p => p
            .Add(x => x.InForm, true)
            .Add(x => x.CaptureFormSubmit, true)
            .Add(x => x.FieldName, "test")
            .Add(x => x.GroupName, "group")
            .Add(x => x.Radios, [new HostRadio("item-a", "a"), new HostRadio("item-b", "b")]));

        await test.SubmitAsync(cut);

        FormValues values = Assert.Single(cut.Instance.FormSubmits);
        Assert.Equal(["test"], values.Keys);
        Assert.Null(values["test"]);
    }

    [Fact(DisplayName = "unblocks submission after every radio in the group unmounts")]
    [Trait("Requirement", "RDG-FRM-09")]
    [Trait("Requirement", "RDG-STA-11")]
    [Trait("Requirement", "RDG-STR-03")]
    [Trait("Requirement", "RAD-FRM-03")]
    public async Task Unblocks_submission_after_every_radio_in_the_group_unmounts()
    {
        HostRadio radio = new("a");
        IRenderedComponent<RadioGroupFieldHost> cut = Context.Render<RadioGroupFieldHost>(p => p
            .Add(x => x.InForm, true)
            .Add(x => x.CaptureFormSubmit, true)
            .Add(x => x.FieldName, "choice")
            .Add(x => x.GroupRequired, true)
            .Add(x => x.Radios, [radio]));
        test.InputValidity = s_missing;

        FormSubmitAnswer blocked = await test.SubmitAsync(cut);
        Assert.Equal(FormSubmitAnswer.BlockedVerdict, blocked.Verdict);
        Assert.Empty(cut.Instance.FormSubmits);

        radio.Mounted = false;
        cut.Render(p => p.Add(x => x.Radios, [radio]));
        await test.SubmitAsync(cut);

        FormValues values = Assert.Single(cut.Instance.FormSubmits);
        Assert.Null(values["choice"]);
    }

    [Fact(DisplayName = "runs the custom validator after every radio in the group unmounts")]
    [Trait("Requirement", "RDG-FRM-09")]
    public async Task Runs_the_custom_validator_after_every_radio_in_the_group_unmounts()
    {
        HostRadio radio = new("a");
        IRenderedComponent<RadioGroupFieldHost> cut = Context.Render<RadioGroupFieldHost>(p => p
            .Add(x => x.InForm, true)
            .Add(x => x.CaptureFormSubmit, true)
            .Add(x => x.FieldName, "choice")
            .Add(x => x.ShowError, true)
            .Add(x => x.ErrorMatch, (MatchValue?)null)
            .Add(x => x.Validate, _ => ValueTask.FromResult<FieldValidationResult?>("always invalid"))
            .Add(x => x.Radios, [radio]));

        radio.Mounted = false;
        cut.Render(p => p.Add(x => x.Radios, [radio]));
        FormSubmitAnswer answer = await test.SubmitAsync(cut);

        Assert.Equal(FormSubmitAnswer.BlockedVerdict, answer.Verdict);
        Assert.Empty(cut.Instance.FormSubmits);
        Assert.Contains("always invalid", cut.Find("[data-testid=error]").TextContent, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "clears external errors on change")]
    [Trait("Requirement", "RDG-FRM-12")]
    [Trait("Requirement", "FFI-FRM-26")]
    public async Task Clears_external_errors_on_change()
    {
        IRenderedComponent<RadioGroupFieldHost> cut = Context.Render<RadioGroupFieldHost>(p => p
            .Add(x => x.InForm, true)
            .Add(x => x.Errors, new FormErrors { ["test"] = "test" })
            .Add(x => x.FieldName, "test")
            .Add(x => x.ShowError, true)
            .Add(x => x.Items, ItemLayout.SiblingLabel)
            .Add(x => x.Radios, [new HostRadio("item-a", "a"), new HostRadio("item-b", "b")]));

        Assert.Contains("test", cut.Find("[data-testid=error]").TextContent, StringComparison.Ordinal);

        await SelectAsync(cut, 0);
        await test.CompleteExitsAsync(cut);

        Assert.Empty(cut.FindAll("[data-testid=error]"));
        Assert.False(cut.Find("[role=radiogroup]").HasAttribute("aria-invalid"));
    }

    [Fact(DisplayName = "appends the id attribute of the error to aria-describedby of individual radios")]
    [Trait("Requirement", "RDG-FRM-14")]
    [Trait("Requirement", "RAD-ARIA-07")]
    public async Task Appends_the_id_attribute_of_the_error_to_aria_describedby_of_individual_radios()
    {
        IRenderedComponent<RadioGroupFieldHost> cut = Context.Render<RadioGroupFieldHost>(p => p
            .Add(x => x.InForm, true)
            .Add(x => x.FieldName, "test")
            .Add(x => x.GroupName, "group")
            .Add(x => x.GroupRequired, true)
            .Add(x => x.ShowError, true)
            .Add(x => x.ErrorMatch, ValidityFlag.ValueMissing)
            .Add(x => x.Items, ItemLayout.SiblingLabel)
            .Add(x => x.Radios, [new HostRadio("a")]));
        Assert.Empty(cut.FindAll("[data-testid=error]"));
        test.InputValidity = s_missing;

        await test.SubmitAsync(cut);

        string errorId = cut.Find("[data-testid=error]").Id!;
        string descriptionId = cut.Find("[data-testid=description]").Id!;
        string describedBy = cut.Find("[role=radio]").GetAttribute("aria-describedby")!;
        Assert.Contains(errorId, describedBy, StringComparison.Ordinal);
        Assert.Contains(descriptionId, describedBy, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "clears required validation when a value is selected")]
    [Trait("Requirement", "RDG-FRM-07")]
    [Trait("Requirement", "RAD-ARIA-08")]
    [Trait("Requirement", "RDG-ARIA-07")]
    public async Task Clears_required_validation_when_a_value_is_selected()
    {
        IRenderedComponent<RadioGroupFieldHost> cut = Context.Render<RadioGroupFieldHost>(p => p
            .Add(x => x.InForm, true)
            .Add(x => x.FieldName, "test")
            .Add(x => x.GroupName, "group")
            .Add(x => x.GroupRequired, true)
            .Add(x => x.ShowError, true)
            .Add(x => x.ErrorMatch, ValidityFlag.ValueMissing)
            .Add(x => x.ErrorText, "required")
            .Add(x => x.Radios, [new HostRadio("item-a", "a"), new HostRadio("item-b", "b")]));
        Assert.Empty(cut.FindAll("[data-testid=error]"));
        test.InputValidity = s_missing;

        await test.SubmitAsync(cut);

        Assert.Contains("required", cut.Find("[data-testid=error]").TextContent, StringComparison.Ordinal);
        Assert.Equal("true", cut.Find("[role=radiogroup]").GetAttribute("aria-invalid"));
        Assert.All(RadioTestContext.Radios(cut), radio => Assert.Equal("true", radio.GetAttribute("aria-invalid")));

        test.InputValidity = NativeValidity.Default;
        await SelectAsync(cut, 1);
        await test.CompleteExitsAsync(cut);

        Assert.Empty(cut.FindAll("[data-testid=error]"));
        Assert.False(cut.Find("[role=radiogroup]").HasAttribute("aria-invalid"));
        Assert.All(RadioTestContext.Radios(cut), radio => Assert.False(radio.HasAttribute("aria-invalid")));
    }

    // RadioGroup.test.tsx "triggers native HTML validation on submit" (L2 half): the required input's valueMissing, read at
    // submit, blocks it and shows the error; focusing the representative radio is the module's (L4).
    [Fact(DisplayName = "triggers native HTML validation on submit")]
    [Trait("Requirement", "RDG-FRM-07")]
    [Trait("Requirement", "RDG-API-06")]
    [Trait("Requirement", "RAD-API-04")]
    [Trait("Requirement", "RAD-FRM-03")]
    [Trait("Requirement", "RDG-FOC-07")]
    public async Task Triggers_native_HTML_validation_on_submit()
    {
        IRenderedComponent<RadioGroupFieldHost> cut = Context.Render<RadioGroupFieldHost>(p => p
            .Add(x => x.InForm, true)
            .Add(x => x.FieldName, "test")
            .Add(x => x.GroupName, "group")
            .Add(x => x.GroupRequired, true)
            .Add(x => x.ShowError, true)
            .Add(x => x.ErrorMatch, ValidityFlag.ValueMissing)
            .Add(x => x.ErrorText, "required")
            .Add(x => x.Items, ItemLayout.SiblingLabel)
            .Add(x => x.Radios, [new HostRadio("item", "a")]));
        Assert.True(cut.Find("input[type=radio]").HasAttribute("required"));
        Assert.Empty(cut.FindAll("[data-testid=error]"));
        test.InputValidity = s_missing;

        FormSubmitAnswer answer = await test.SubmitAsync(cut);

        Assert.Equal(FormSubmitAnswer.BlockedVerdict, answer.Verdict);
        Assert.Equal([cut.Find("[data-testid=item]").Id!], answer.Focus);
        Assert.Contains("required", cut.Find("[data-testid=error]").TextContent, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "port: the focus target is the first eligible, natively invalid radio's root, else the first eligible one")]
    [Trait("Requirement", "RDG-FOC-07")]
    [Trait("Requirement", "FFI-FOC-04")]
    public async Task Port_the_focus_target_is_the_representative_radios_root()
    {
        IRenderedComponent<RadioGroupFieldHost> cut = Context.Render<RadioGroupFieldHost>(p => p
            .Add(x => x.InForm, true)
            .Add(x => x.FieldName, "test")
            .Add(x => x.Validate, _ => ValueTask.FromResult<FieldValidationResult?>("invalid"))
            .Add(x => x.Radios, [new HostRadio("a") { Disabled = true }, new HostRadio("b"), new HostRadio("c")]));
        IReadOnlyList<IElement> radios = RadioTestContext.Radios(cut);
        test.InputValidityById[RadioTestContext.Inputs(cut)[2].Id!] = s_missing;

        FormSubmitAnswer answer = await test.SubmitAsync(cut);

        Assert.Equal([radios[2].Id!], answer.Focus);

        test.InputValidityById.Clear();
        answer = await test.SubmitAsync(cut);
        Assert.Equal([radios[1].Id!], answer.Focus);
    }

    [Fact(DisplayName = "excludes a disabled selected radio from onFormSubmit to match native form data")]
    [Trait("Requirement", "RDG-FRM-02")]
    [Trait("Requirement", "RDG-FRM-06")]
    public async Task Excludes_a_disabled_selected_radio_from_onFormSubmit()
    {
        HostRadio a = new("item-a", "a");
        IRenderedComponent<RadioGroupFieldHost> cut = RenderFormProjection(a, out _);

        a.Disabled = true;
        cut.Render(p => p.Add(x => x.Radios, [a, new HostRadio("item-b", "b")]));
        await test.SettleAllFormsAsync(cut);
        await test.SubmitAsync(cut);

        Assert.Null(Assert.Single(cut.Instance.FormSubmits)["test"]);
    }

    [Fact(DisplayName = "includes a selected radio again when it is re-enabled before form submission")]
    [Trait("Requirement", "RDG-FRM-02")]
    [Trait("Requirement", "RDG-FRM-06")]
    public async Task Includes_a_selected_radio_again_when_it_is_re_enabled()
    {
        HostRadio a = new("item-a", "a");
        IRenderedComponent<RadioGroupFieldHost> cut = RenderFormProjection(a, out HostRadio b);

        a.Disabled = true;
        cut.Render(p => p.Add(x => x.Radios, [a, b]));
        a.Disabled = false;
        cut.Render(p => p.Add(x => x.Radios, [a, b]));
        await test.SettleAllFormsAsync(cut);
        await test.SubmitAsync(cut);

        Assert.Equal("a", Assert.Single(cut.Instance.FormSubmits)["test"]);
    }

    [Fact(DisplayName = "excludes an initially disabled selected radio from onFormSubmit to match native form data")]
    [Trait("Requirement", "RDG-FRM-02")]
    [Trait("Requirement", "RDG-FRM-06")]
    public async Task Excludes_an_initially_disabled_selected_radio_from_onFormSubmit()
    {
        HostRadio a = new("item-a", "a") { Disabled = true };
        IRenderedComponent<RadioGroupFieldHost> cut = RenderFormProjection(a, out _);

        await test.SubmitAsync(cut);

        Assert.Null(Assert.Single(cut.Instance.FormSubmits)["test"]);
    }

    [Fact(DisplayName = "port: outside a form the projected value is the raw value")]
    [Trait("Requirement", "RDG-FRM-06")]
    public async Task Port_outside_a_form_the_projected_value_is_the_raw_value()
    {
        object? projected = "unset";
        IRenderedComponent<RadioGroupFieldHost> cut = Context.Render<RadioGroupFieldHost>(p => p
            .Add(x => x.FieldName, "choice")
            .Add(x => x.ValidationMode, ValidationMode.OnChange)
            .Add(x => x.Validate, args =>
            {
                projected = args.Values["choice"];
                return ValueTask.FromResult<FieldValidationResult?>(null);
            })
            .Add(x => x.Radios, [new HostRadio("a") { Disabled = true }, new HostRadio("b")]));

        await SelectAsync(cut, 1);

        Assert.Equal("b", projected);
    }

    private IRenderedComponent<RadioGroupFieldHost> RenderFormProjection(HostRadio a, out HostRadio b)
    {
        HostRadio second = new("item-b", "b");
        b = second;
        return Context.Render<RadioGroupFieldHost>(p => p
            .Add(x => x.InForm, true)
            .Add(x => x.CaptureFormSubmit, true)
            .Add(x => x.FieldName, "test")
            .Add(x => x.GroupName, "group")
            .Add(x => x.DefaultValue, "a")
            .Add(x => x.Radios, [a, second]));
    }

    // A click on radio `index`, as the module reports it on Server, then the refreshes the commit starts.
    private async Task SelectAsync<TComponent>(IRenderedComponent<TComponent> cut, int index)
        where TComponent : IComponent
    {
        await test.ToggleAsync(cut, index);
        await test.SettleAllFormsAsync(cut);
    }
}
