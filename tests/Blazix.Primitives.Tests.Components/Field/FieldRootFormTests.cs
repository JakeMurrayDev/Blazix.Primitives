using Bunit;
using Microsoft.AspNetCore.Components;
using Scenario = Blazix.Primitives.Tests.Components.FieldFormScenarios.FormScenario;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The <c>[L2]</c> titles of <c>FieldRoot.test.tsx</c> and <c>FieldRoot.react17.test.tsx</c> that render <c>Form</c>,
/// <c>Field.Error</c> or <c>Field.Validity</c> (PR 1c, the landing rule of issue 77). "drops a pending validation when
/// another field-aware control takes ownership" renders <c>Switch.Root</c> in the field and waits for Switch's field rows
/// (issue 80).
/// </summary>
public sealed class FieldRootFormTests : IDisposable
{
    private static readonly string[] s_parts = ["root", "control", "label", "description"];

    private readonly FieldTestContext host = new();

    private BunitContext Context => host.Context;

    /// <inheritdoc />
    public void Dispose() => host.Dispose();

    [Fact(DisplayName = "keeps a disabled field with form errors marked invalid")]
    [Trait("Requirement", "FLD-STA-09")]
    [Trait("Requirement", "FLD-FRM-09")]
    [Trait("Requirement", "FRM-FRM-07")]
    [Trait("Requirement", "FFI-STA-02")]
    public void Keeps_a_disabled_field_with_form_errors_marked_invalid()
    {
        IRenderedComponent<FieldFormScenarios> cut = Render(Scenario.DisabledWithFormError, p => p.Add(x => x.Errors, new FormErrors { ["name"] = "Server error" }));

        Assert.True(cut.Find("[data-testid=control]").HasAttribute("data-invalid"));
        Assert.False(cut.Find("[data-testid=control]").HasAttribute("aria-invalid"));
    }

    [Fact(DisplayName = "should apply aria-invalid prop to control once validation finishes")]
    [Trait("Requirement", "FLD-ARIA-03")]
    [Trait("Requirement", "FFI-ARIA-03")]
    [Trait("Requirement", "FFI-FRM-16")]
    public void Should_apply_aria_invalid_to_control_once_validation_finishes()
    {
        IRenderedComponent<FieldFormScenarios> cut = Render(Scenario.SubmitValidation, p => p.Add(x => x.Validate, _ => new ValueTask<FieldValidationResult?>("error")));
        Assert.False(cut.Find("[data-testid=control]").HasAttribute("aria-invalid"));

        host.Module.FormElement.SubmitAll(cut);

        Assert.Equal("true", cut.Find("[data-testid=control]").GetAttribute("aria-invalid"));
    }

    [Fact(DisplayName = "unmounted fields are excluded from the validate fn")]
    [Trait("Requirement", "FLD-EVT-08")]
    [Trait("Requirement", "FLD-FRM-01")]
    [Trait("Requirement", "FFI-EVT-05")]
    public void Unmounted_fields_are_excluded_from_the_validate_fn()
    {
        List<FormValues> calls = [];
        IRenderedComponent<FieldFormScenarios> cut = Render(Scenario.Unmounted, p => p.Add(x => x.Validate, arguments =>
        {
            calls.Add(arguments.Values);
            return new ValueTask<FieldValidationResult?>((FieldValidationResult?)null);
        }));

        host.Module.FormElement.SubmitAll(cut);
        FormValues first = Assert.Single(calls);
        Assert.Equal(["input1", "input2"], first.Keys);
        Assert.Equal("one", first["input1"]);
        Assert.Equal("two", first["input2"]);

        cut.Render(p => p.Add(x => x.Checked, false));
        host.Module.FormElement.SubmitAll(cut);

        Assert.Equal(2, calls.Count);
        Assert.Equal(["input2"], calls[^1].Keys);
        Assert.Equal(2, cut.Instance.Submits.Count);
    }

    [Fact(DisplayName = "uses the Field.Control name for form submission and form validation values")]
    [Trait("Requirement", "FLD-API-01")]
    [Trait("Requirement", "FLD-API-18")]
    [Trait("Requirement", "FLD-FRM-01")]
    [Trait("Requirement", "FFI-FRM-33")]
    public void Uses_the_Field_Control_name_for_form_submission_and_form_validation_values()
    {
        List<FormValues> calls = [];
        IRenderedComponent<FieldFormScenarios> cut = Render(Scenario.ControlName, p => p.Add(x => x.Validate, arguments =>
        {
            calls.Add(arguments.Values);
            return new ValueTask<FieldValidationResult?>((FieldValidationResult?)null);
        }));

        host.Module.FormElement.SubmitAll(cut);

        Assert.Equal(["email", "confirmEmail"], calls[^1].Keys);
        Assert.Equal(["one@example.com", "one@example.com"], calls[^1].Values);
        FormSubmitEventArgs args = Assert.Single(cut.Instance.FormSubmits);
        Assert.Equal(["email", "confirmEmail"], args.Values.Keys);
        Assert.Equal(["one@example.com", "one@example.com"], args.Values.Values);
    }

    [Fact(DisplayName = "updates the Field.Control name fallback when the name changes")]
    [Trait("Requirement", "FLD-API-18")]
    [Trait("Requirement", "FLD-FRM-02")]
    [Trait("Requirement", "FLD-FRM-15")]
    [Trait("Requirement", "FFI-STA-10")]
    public void Updates_the_Field_Control_name_fallback_when_the_name_changes()
    {
        IRenderedComponent<FieldFormScenarios> cut = Render(Scenario.SubmitValidation, p => p.Add(x => x.ControlName, "email").Add(x => x.DefaultValue, "one@example.com"));

        host.Module.FormElement.SubmitAll(cut);
        Assert.Equal(["email"], cut.Instance.FormSubmits[^1].Values.Keys);

        cut.Render(p => p.Add(x => x.ControlName, "alternateEmail"));
        host.Module.FormElement.SubmitAll(cut);
        Assert.Equal(["alternateEmail"], cut.Instance.FormSubmits[^1].Values.Keys);
        Assert.Equal("one@example.com", cut.Instance.FormSubmits[^1].Values["alternateEmail"]);

        cut.Render(p => p.Add(x => x.ControlName, null));
        host.Module.FormElement.SubmitAll(cut);
        Assert.Empty(cut.Instance.FormSubmits[^1].Values);
    }

    [Fact(DisplayName = "uses the Field.Control name fallback when the Field.Root name is removed")]
    [Trait("Requirement", "FLD-API-01")]
    [Trait("Requirement", "FLD-FRM-09")]
    [Trait("Requirement", "FLD-FRM-15")]
    [Trait("Requirement", "FFI-STA-10")]
    public void Uses_the_Field_Control_name_fallback_when_the_Field_Root_name_is_removed()
    {
        IRenderedComponent<FieldFormScenarios> cut = Render(Scenario.SubmitValidation, p => p
            .Add(x => x.Errors, new FormErrors { ["email"] = "Email is already taken" })
            .Add(x => x.RootName, "rootEmail")
            .Add(x => x.ControlName, "email"));

        Assert.False(cut.Find("[data-testid=control]").HasAttribute("aria-invalid"));
        Assert.Empty(cut.FindAll("[data-testid=error]"));

        cut.Render(p => p.Add(x => x.RootName, null));

        Assert.Equal("true", cut.Find("[data-testid=control]").GetAttribute("aria-invalid"));
        Assert.Equal("Email is already taken", cut.Find("[data-testid=error]").TextContent);
    }

    [Fact(DisplayName = "does not publish errors while `valueMissing` is suppressed")]
    [Trait("Requirement", "FLD-FRM-05")]
    [Trait("Requirement", "FLD-STA-07")]
    [Trait("Requirement", "FLD-STR-05")]
    [Trait("Requirement", "FFI-FRM-06")]
    public void Does_not_publish_errors_while_valueMissing_is_suppressed()
    {
        IRenderedComponent<FieldFormScenarios> cut = Render(Scenario.Parts, p => p.Add(x => x.ValidationMode, ValidationMode.OnBlur).Add(x => x.Required, true));
        FormModuleStandIn.Attached control = host.Module.Control;
        control.Native = Native.ValueMissing;

        control.Focused(cut);
        control.Blur(cut);

        FieldValidityState latest = cut.Instance.ValidityCalls[^1];
        Assert.True(latest.Validity.Valid);
        Assert.Empty(latest.Errors);
        Assert.Equal(string.Empty, latest.Error);
    }

    [Fact(DisplayName = "should apply [data-field] style hooks to field components")]
    [Trait("Requirement", "FLD-API-30")]
    [Trait("Requirement", "FLD-DOM-03")]
    [Trait("Requirement", "FLD-DOM-04")]
    [Trait("Requirement", "FLD-DOM-14")]
    [Trait("Requirement", "FFI-DOM-01")]
    public void Should_apply_data_field_style_hooks_to_field_components()
    {
        IRenderedComponent<FieldFormScenarios> cut = Render(Scenario.Parts, p => p.Add(x => x.ValidationMode, ValidationMode.OnBlur).Add(x => x.Required, true));
        FormModuleStandIn.Attached control = host.Module.Control;
        foreach (string part in new[] { "control", "label", "description" })
        {
            Assert.False(cut.Find($"[data-testid={part}]").HasAttribute("data-valid"));
        }

        Assert.Empty(cut.FindAll("[data-testid=error]"));

        control.Focused(cut);
        control.Input(cut, "a", Native.Valid);
        control.Input(cut, string.Empty, Native.ValueMissing);
        control.Blur(cut);

        foreach (string part in new[] { "control", "label", "description", "error" })
        {
            Assert.True(cut.Find($"[data-testid={part}]").HasAttribute("data-invalid"), part);
        }

        control.DomValue = "value";
        control.Native = Native.Valid;
        control.Focused(cut);
        control.Blur(cut);
        host.CompleteExits(cut);

        foreach (string part in new[] { "control", "label", "description" })
        {
            Assert.True(cut.Find($"[data-testid={part}]").HasAttribute("data-valid"), part);
        }

        Assert.Empty(cut.FindAll("[data-testid=error]"));
    }

    [Fact(DisplayName = "should apply [data-touched] style hook to all components when touched")]
    [Trait("Requirement", "FLD-DOM-02")]
    [Trait("Requirement", "FLD-DOM-04")]
    [Trait("Requirement", "FLD-FOC-02")]
    [Trait("Requirement", "FFI-STA-07")]
    public void Should_apply_data_touched_to_all_components_when_touched()
    {
        IRenderedComponent<FieldFormScenarios> cut = Render(Scenario.Parts);
        AssertParts(cut, "data-touched", false);
        Assert.Empty(cut.FindAll("[data-testid=error]"));

        host.Module.Control.Focused(cut);
        host.Module.Control.Blur(cut);

        AssertParts(cut, "data-touched", true);
        Assert.Empty(cut.FindAll("[data-testid=error]"));
    }

    [Fact(DisplayName = "should apply [data-dirty] style hook to all components when dirty")]
    [Trait("Requirement", "FLD-DOM-02")]
    [Trait("Requirement", "FLD-DOM-04")]
    [Trait("Requirement", "FLD-STA-14")]
    [Trait("Requirement", "FFI-STA-05")]
    public void Should_apply_data_dirty_to_all_components_when_dirty()
    {
        IRenderedComponent<FieldFormScenarios> cut = Render(Scenario.Parts);
        AssertParts(cut, "data-dirty", false);

        host.Module.Control.Input(cut, "value");
        AssertParts(cut, "data-dirty", true);

        host.Module.Control.Input(cut, string.Empty);
        AssertParts(cut, "data-dirty", false);
    }

    [Fact(DisplayName = "should apply [data-filled] style hook to all components when filled")]
    [Trait("Requirement", "FLD-DOM-02")]
    [Trait("Requirement", "FLD-DOM-04")]
    [Trait("Requirement", "FLD-STA-11")]
    [Trait("Requirement", "FFI-STA-08")]
    public void Should_apply_data_filled_to_all_components_when_filled()
    {
        IRenderedComponent<FieldFormScenarios> cut = Render(Scenario.Parts);
        AssertParts(cut, "data-filled", false);

        host.Module.Control.Input(cut, "value");
        AssertParts(cut, "data-filled", true);

        host.Module.Control.Input(cut, string.Empty);
        AssertParts(cut, "data-filled", false);
    }

    [Fact(DisplayName = "should apply [data-focused] style hook to all components when focused")]
    [Trait("Requirement", "FLD-DOM-02")]
    [Trait("Requirement", "FLD-DOM-04")]
    [Trait("Requirement", "FLD-FOC-01")]
    [Trait("Requirement", "FLD-FOC-02")]
    public void Should_apply_data_focused_to_all_components_when_focused()
    {
        IRenderedComponent<FieldFormScenarios> cut = Render(Scenario.Parts);
        AssertParts(cut, "data-focused", false);

        host.Module.Control.Focused(cut);
        AssertParts(cut, "data-focused", true);

        host.Module.Control.Blur(cut);
        AssertParts(cut, "data-focused", false);
    }

    [Fact(DisplayName = "controls the dirty state")]
    [Trait("Requirement", "FLD-API-07")]
    [Trait("Requirement", "FLD-DOM-04")]
    [Trait("Requirement", "FFI-API-05")]
    public void Controls_the_dirty_state()
    {
        IRenderedComponent<FieldFormScenarios> cut = Render(Scenario.Parts, p => p.Add(x => x.Dirty, true));

        AssertParts(cut, "data-dirty", true);
    }

    [Fact(DisplayName = "controls the touched state")]
    [Trait("Requirement", "FLD-API-07")]
    [Trait("Requirement", "FLD-DOM-04")]
    [Trait("Requirement", "FFI-API-05")]
    public void Controls_the_touched_state()
    {
        IRenderedComponent<FieldFormScenarios> cut = Render(Scenario.Parts, p => p.Add(x => x.Touched, true));

        AssertParts(cut, "data-touched", true);
    }

    [Fact(DisplayName = "validates the field when the `validate` method is called")]
    [Trait("Requirement", "FLD-API-08")]
    [Trait("Requirement", "FFI-API-06")]
    public async Task Validates_the_field_when_the_validate_method_is_called()
    {
        IRenderedComponent<FieldFormScenarios> cut = Render(Scenario.Actions, p => p.Add(x => x.RootName, "username").Add(x => x.DefaultValue, string.Empty).Add(x => x.Required, true));
        host.Module.Control.Native = Native.ValueMissing;
        Assert.Empty(cut.FindAll("[data-testid=error]"));

        await Validate(cut);

        Assert.Single(cut.FindAll("[data-testid=error]"));
    }

    [Fact(DisplayName = "validates a logical field without a mounted control")]
    [Trait("Requirement", "FLD-API-08")]
    [Trait("Requirement", "FLD-FRM-12")]
    [Trait("Requirement", "FFI-ALG-12")]
    [Trait("Requirement", "FFI-FRM-21")]
    public async Task Validates_a_logical_field_without_a_mounted_control()
    {
        IRenderedComponent<FieldFormScenarios> cut = Render(Scenario.Actions, p => p
            .Add(x => x.ControlMounted, false)
            .Add(x => x.Validate, _ => new ValueTask<FieldValidationResult?>("Logical field error")));

        await Validate(cut);

        Assert.Equal("Logical field error", cut.Find("[data-testid=error]").TextContent);
    }

    [Fact(DisplayName = "validates the current control value when the `validate` method is called")]
    [Trait("Requirement", "FLD-API-08")]
    [Trait("Requirement", "FLD-EVT-08")]
    [Trait("Requirement", "FFI-FRM-21")]
    public async Task Validates_the_current_control_value_when_the_validate_method_is_called()
    {
        List<object?> validated = [];
        IRenderedComponent<FieldFormScenarios> cut = Render(Scenario.Actions, p => p.Add(x => x.Validate, arguments =>
        {
            validated.Add(arguments.Value);
            return new ValueTask<FieldValidationResult?>(arguments.Value as string == "valid" ? null : "error");
        }));

        host.Module.Control.Input(cut, "valid");
        await Validate(cut);

        Assert.Equal("valid", validated[^1]);
        Assert.Empty(cut.FindAll("[data-testid=error]"));
    }

    // FFI-STR-05 (fidelity correction 2026-10-23, settled by the orchestrator 2026-10-23: ADR 15; COL-DOM-05 precedent): Base
    // UI's default form context holds one fields map that every field outside a Form registers in; the port's is one per
    // runtime (a scoped service), so a formless validator sees every named formless field of the circuit or application.
    [Fact(DisplayName = "a formless field's validator sees every named field outside a form in the runtime")]
    [Trait("Requirement", "FFI-STR-05")]
    [Trait("Requirement", "FFI-API-07")]
    public void A_formless_fields_validator_sees_every_named_field_outside_a_form()
    {
        List<FormValues> calls = [];
        IRenderedComponent<FieldFormScenarios> cut = Render(Scenario.Formless, p => p
            .Add(x => x.ValidationMode, ValidationMode.OnChange)
            .Add(x => x.Validate, arguments =>
            {
                calls.Add(arguments.Values);
                return new ValueTask<FieldValidationResult?>((FieldValidationResult?)null);
            }));

        host.Module.Controls[0].Input(cut, "changed");

        FormValues values = Assert.Single(calls);
        Assert.Equal(["first", "second"], values.Keys);
        Assert.Equal(["changed", "two"], values.Values);
    }

    [Fact(DisplayName = "allows mount-time imperative validation before the fallback id is assigned")]
    [Trait("Requirement", "FLD-API-08")]
    [Trait("Requirement", "FLD-FRM-12")]
    [Trait("Requirement", "FFI-EDGE-10")]
    [Trait("Requirement", "FFI-FRM-21")]
    public void Allows_mount_time_imperative_validation_before_the_fallback_id_is_assigned()
    {
        IRenderedComponent<FieldFormScenarios> cut = Render(Scenario.MountValidation, p => p.Add(x => x.Validate, _ => new ValueTask<FieldValidationResult?>("Mount-time error")));

        cut.WaitForAssertion(() => Assert.Equal("Mount-time error", cut.Find("[data-testid=error]").TextContent));
    }

    // Base UI's button calls actionsRef.validate(); the port's FieldRoot.ValidateAsync first asks the control for a fresh
    // report, which the stand-in module answers.
    private async Task Validate(IRenderedComponent<FieldFormScenarios> cut)
    {
        Task validation = cut.InvokeAsync(cut.Instance.Root.ValidateAsync);
        host.Module.Settle(cut);
        await validation;
    }

    private static void AssertParts(IRenderedComponent<FieldFormScenarios> cut, string attribute, bool present)
    {
        foreach (string part in s_parts)
        {
            Assert.True(cut.Find($"[data-testid={part}]").HasAttribute(attribute) == present, $"{part} {attribute}");
        }
    }

    private IRenderedComponent<FieldFormScenarios> Render(Scenario scenario, Action<ComponentParameterCollectionBuilder<FieldFormScenarios>>? parameters = null) =>
        Context.Render<FieldFormScenarios>(p =>
        {
            p.Add(x => x.Scenario, scenario);
            parameters?.Invoke(p);
        });
}
