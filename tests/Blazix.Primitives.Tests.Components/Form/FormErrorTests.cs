using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Scenario = Blazix.Primitives.Tests.Components.FormErrorScenarios.ErrorScenario;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The <c>[L2]</c> titles of <c>Form.test.tsx</c> that render <c>Field.Error</c> over <c>Field.Control</c> (PR 1c; the four
/// that render <c>Switch.Root</c> wait for Switch's field rows, issue 80). A native submission is the stand-in module's
/// report with every control's state; an error's exit ends when the test reports it, as Base UI's jsdom tests run with
/// animations disabled.
/// </summary>
public sealed class FormErrorTests : IDisposable
{
    private readonly FieldTestContext host = new();

    private BunitContext Context => host.Context;

    /// <inheritdoc />
    public void Dispose() => host.Dispose();

    public static TheoryData<ValidationMode> BlurAndChange => new() { ValidationMode.OnBlur, ValidationMode.OnChange };

    [Fact(DisplayName = "does not submit if there are errors")]
    [Trait("Requirement", "FRM-EVT-01")]
    [Trait("Requirement", "FRM-EVT-05")]
    [Trait("Requirement", "FFI-EVT-01")]
    public void Does_not_submit_if_there_are_errors()
    {
        IRenderedComponent<FormErrorScenarios> cut = Render(Scenario.Required);
        host.Module.Control.Native = Native.ValueMissing;

        Assert.Equal("blocked", host.Module.FormElement.SubmitAll(cut).Verdict);

        Assert.Single(cut.FindAll("[data-testid=error]"));
        Assert.Empty(cut.Instance.Submits);
    }

    [Fact(DisplayName = "re-runs an onBlur cross-field validator on submit")]
    [Trait("Requirement", "FRM-FRM-01")]
    [Trait("Requirement", "FFI-EVT-05")]
    [Trait("Requirement", "FFI-FRM-04")]
    public void Re_runs_an_onBlur_cross_field_validator_on_submit()
    {
        int calls = 0;
        IRenderedComponent<FormErrorScenarios> cut = Render(Scenario.CrossField, p => p.Add(x => x.Validate, arguments =>
        {
            calls++;
            return new ValueTask<FieldValidationResult?>(!Equals(arguments.Value, arguments.Values["password"]) ? "Passwords do not match" : null);
        }));
        FormModuleStandIn.Attached password = host.Module.Controls[0];
        FormModuleStandIn.Attached confirm = host.Module.Controls[1];

        password.Input(cut, "secret");
        confirm.Input(cut, "typo");
        confirm.Blur(cut);

        Assert.Equal(1, calls);
        Assert.Equal("Passwords do not match", cut.Find("[data-testid=confirm-error]").TextContent);

        password.Input(cut, "typo");
        password.Blur(cut);
        host.Module.FormElement.SubmitAll(cut);
        host.CompleteExits(cut);

        Assert.Equal(2, calls);
        Assert.Empty(cut.FindAll("[data-testid=confirm-error]"));
        Assert.Single(cut.Instance.FormSubmits);
    }

    [Fact(DisplayName = "retires a stale async validation result on the next submit")]
    [Trait("Requirement", "FRM-FRM-02")]
    [Trait("Requirement", "FFI-FRM-14")]
    public void Retires_a_stale_async_validation_result_on_the_next_submit()
    {
        List<(object? Value, TaskCompletionSource<FieldValidationResult?> Result)> pending = [];
        IRenderedComponent<FormErrorScenarios> cut = Render(Scenario.Single, p => p
            .Add(x => x.Name, "field")
            .Add(x => x.Mode, ValidationMode.OnSubmit)
            .Add(x => x.DefaultValue, "bad")
            .Add(x => x.Validate, arguments =>
            {
                TaskCompletionSource<FieldValidationResult?> result = new();
                pending.Add((arguments.Value, result));
                return new ValueTask<FieldValidationResult?>(result.Task);
            }));

        host.Module.FormElement.SubmitAll(cut);
        Assert.Single(cut.Instance.FormSubmits);

        Resolve(cut, pending, "bad", "async error");
        Assert.Equal("async error", cut.Find("[data-testid=error]").TextContent);

        host.Module.Control.Input(cut, "good");
        host.Module.FormElement.SubmitAll(cut);
        host.CompleteExits(cut);

        Assert.Equal(2, cut.Instance.FormSubmits.Count);
        Assert.Empty(cut.FindAll("[data-testid=error]"));
    }

    [Theory(DisplayName = "blocks submission on a resolved async error in $validationMode mode")]
    [MemberData(nameof(BlurAndChange))]
    [Trait("Requirement", "FRM-FRM-02")]
    [Trait("Requirement", "FRM-API-01")]
    [Trait("Requirement", "FFI-FRM-14")]
    public void Blocks_submission_on_a_resolved_async_error(ValidationMode mode)
    {
        List<(object? Value, TaskCompletionSource<FieldValidationResult?> Result)> pending = [];
        IRenderedComponent<FormErrorScenarios> cut = Render(Scenario.Single, p => p
            .Add(x => x.Name, "username")
            .Add(x => x.Mode, mode)
            .Add(x => x.Validate, arguments =>
            {
                TaskCompletionSource<FieldValidationResult?> result = new();
                pending.Add((arguments.Value, result));
                return new ValueTask<FieldValidationResult?>(result.Task);
            }));

        host.Module.Control.Input(cut, "taken");
        host.Module.Control.Blur(cut);
        Resolve(cut, pending, "taken", "Username is taken");
        Assert.Equal("Username is taken", cut.Find("[data-testid=error]").TextContent);

        Assert.Equal("blocked", host.Module.FormElement.SubmitAll(cut).Verdict);

        Assert.Empty(cut.Instance.FormSubmits);
        Assert.Equal("Username is taken", cut.Find("[data-testid=error]").TextContent);
    }

    [Fact(DisplayName = "clears invalid UI when a fieldset field becomes disabled")]
    [Trait("Requirement", "FRM-FRM-04")]
    [Trait("Requirement", "FST-FRM-01")]
    [Trait("Requirement", "FFI-STA-02")]
    public void Clears_invalid_UI_when_a_fieldset_field_becomes_disabled()
    {
        IRenderedComponent<FormErrorScenarios> cut = Render(Scenario.Fieldset);
        host.Module.Control.Native = Native.ValueMissing;

        host.Module.FormElement.SubmitAll(cut);
        Assert.Empty(cut.Instance.FormSubmits);
        Assert.Equal("true", cut.Find("[data-testid=control]").GetAttribute("aria-invalid"));
        Assert.Single(cut.FindAll("[data-testid=error]"));

        cut.Render(p => p.Add(x => x.Disabled, true));
        host.CompleteExits(cut);
        Assert.True(cut.Find("[data-testid=control]").HasAttribute("disabled"));
        Assert.False(cut.Find("[data-testid=control]").HasAttribute("aria-invalid"));
        Assert.Empty(cut.FindAll("[data-testid=error]"));

        host.Module.FormElement.SubmitAll(cut);
        Assert.Empty(Assert.Single(cut.Instance.FormSubmits).Values);

        cut.Render(p => p.Add(x => x.Disabled, false));
        host.Module.FormElement.SubmitAll(cut);
        Assert.Single(cut.Instance.FormSubmits);
        Assert.Equal("true", cut.Find("[data-testid=control]").GetAttribute("aria-invalid"));
        Assert.Single(cut.FindAll("[data-testid=error]"));
    }

    [Fact(DisplayName = "should mark <Field.Control> as invalid and populate <Field.Error>")]
    [Trait("Requirement", "FRM-API-02")]
    [Trait("Requirement", "FRM-FRM-05")]
    [Trait("Requirement", "FFI-FRM-25")]
    public void Should_mark_Field_Control_as_invalid_and_populate_Field_Error()
    {
        IRenderedComponent<FormErrorScenarios> cut = Render(Scenario.Single, p => p.Add(x => x.Name, "foo").Add(x => x.Errors, new FormErrors { ["foo"] = "bar" }));

        Assert.Equal("bar", cut.Find("[data-testid=error]").TextContent);
        Assert.Equal("true", cut.Find("[data-testid=control]").GetAttribute("aria-invalid"));
    }

    [Fact(DisplayName = "should not mark <Field.Control> as invalid if no error is provided")]
    [Trait("Requirement", "FRM-FRM-05")]
    [Trait("Requirement", "FRM-EDGE-03")]
    public void Should_not_mark_Field_Control_as_invalid_if_no_error_is_provided()
    {
        IRenderedComponent<FormErrorScenarios> cut = Render(Scenario.Single, p => p.Add(x => x.Name, "foo"));

        Assert.Empty(cut.FindAll("[data-testid=error]"));
        Assert.False(cut.Find("[data-testid=control]").HasAttribute("aria-invalid"));
    }

    // The focus itself is the module's (L4, form.spec.ts); here the request it receives after the render.
    [Fact(DisplayName = "focuses asynchronously replaced external errors and clears only the changed own property")]
    [Trait("Requirement", "FRM-FOC-02")]
    [Trait("Requirement", "FRM-EVT-06")]
    [Trait("Requirement", "FRM-EDGE-03")]
    [Trait("Requirement", "FFI-EVT-04")]
    public void Focuses_asynchronously_replaced_external_errors_and_clears_only_the_changed_own_property()
    {
        IRenderedComponent<FormErrorScenarios> cut = Render(Scenario.TwoFields);

        Assert.Equal("submit", host.Module.FormElement.SubmitAll(cut).Verdict);
        cut.Instance.ApplyPendingErrors();

        FormSubmitOptions focus = host.Module.FormElement.SubmitUpdates()[^1];
        Assert.Equal(1, focus.FocusRevision);
        Assert.Equal([cut.Find("[data-testid=first]").GetAttribute("id")!, cut.Find("[data-testid=second]").GetAttribute("id")!], focus.Focus);
        Assert.Equal("First error", cut.Find("[data-testid=first-error]").TextContent);
        Assert.Equal("Second error", cut.Find("[data-testid=second-error]").TextContent);

        host.Module.Controls[0].Input(cut, "a");
        host.CompleteExits(cut);

        Assert.Empty(cut.FindAll("[data-testid=first-error]"));
        Assert.Equal("Second error", cut.Find("[data-testid=second-error]").TextContent);
    }

    [Fact(DisplayName = "runs field validation on first change after Form error is set")]
    [Trait("Requirement", "FRM-FRM-06")]
    [Trait("Requirement", "FRM-STA-03")]
    [Trait("Requirement", "FFI-FRM-26")]
    public void Runs_field_validation_on_first_change_after_Form_error_is_set()
    {
        int calls = 0;
        IRenderedComponent<FormErrorScenarios> cut = Render(Scenario.ServerErrors, p => p
            .Add(x => x.Errors, new FormErrors())
            .Add(x => x.Validate, arguments =>
            {
                calls++;
                return new ValueTask<FieldValidationResult?>(arguments.Value as string == "abcd" ? "field error" : null);
            }));
        FormModuleStandIn.Attached name = host.Module.Control;

        name.Focused(cut);
        name.Input(cut, "abcde");
        host.Module.FormElement.SubmitAll(cut);
        Assert.Equal("submit error", cut.Find("[data-testid=name-error]").TextContent);

        calls = 0;
        name.Input(cut, "abcd");

        Assert.Equal(1, calls);
        Assert.Equal("field error", cut.Find("[data-testid=name-error]").TextContent);
    }

    [Fact(DisplayName = "runs field validation on change when invalid prop is true and validationMode is onChange")]
    [Trait("Requirement", "FRM-FRM-06")]
    [Trait("Requirement", "FFI-FRM-26")]
    public void Runs_field_validation_on_change_when_invalid_prop_is_true_and_validationMode_is_onChange()
    {
        int calls = 0;
        IRenderedComponent<FormErrorScenarios> cut = Render(Scenario.Single, p => p
            .Add(x => x.Name, "name")
            .Add(x => x.Invalid, true)
            .Add(x => x.Mode, ValidationMode.OnChange)
            .Add(x => x.Errors, new FormErrors { ["name"] = "server error" })
            .Add(x => x.Validate, _ =>
            {
                calls++;
                return new ValueTask<FieldValidationResult?>("field error");
            }));
        Assert.Equal("server error", cut.Find("[data-testid=error]").TextContent);

        host.Module.Control.Focused(cut);
        host.Module.Control.Input(cut, "a");

        Assert.Equal(1, calls);
        Assert.Equal("field error", cut.Find("[data-testid=error]").TextContent);
        Assert.Equal("true", cut.Find("[data-testid=control]").GetAttribute("aria-invalid"));
    }

    [Fact(DisplayName = "does not run field validation on change for onBlur mode when invalid prop is true")]
    [Trait("Requirement", "FRM-FRM-06")]
    [Trait("Requirement", "FFI-FRM-26")]
    public void Does_not_run_field_validation_on_change_for_onBlur_mode_when_invalid_prop_is_true()
    {
        int calls = 0;
        IRenderedComponent<FormErrorScenarios> cut = Render(Scenario.Single, p => p
            .Add(x => x.Name, "name")
            .Add(x => x.Invalid, true)
            .Add(x => x.Mode, ValidationMode.OnBlur)
            .Add(x => x.Errors, new FormErrors { ["name"] = "server error" })
            .Add(x => x.Validate, _ =>
            {
                calls++;
                return new ValueTask<FieldValidationResult?>("field error");
            }));
        Assert.Equal("server error", cut.Find("[data-testid=error]").TextContent);

        host.Module.Control.Focused(cut);
        host.Module.Control.Input(cut, "a");
        host.CompleteExits(cut);
        Assert.Equal(0, calls);
        Assert.Empty(cut.FindAll("[data-testid=error]"));

        host.Module.Control.Blur(cut);
        Assert.Equal(1, calls);
        Assert.Equal("field error", cut.Find("[data-testid=error]").TextContent);
    }

    [Fact(DisplayName = "does not run when the form is invalid")]
    [Trait("Requirement", "FRM-EVT-01")]
    [Trait("Requirement", "FRM-EVT-05")]
    [Trait("Requirement", "FFI-EVT-01")]
    public void Does_not_run_when_the_form_is_invalid()
    {
        IRenderedComponent<FormErrorScenarios> cut = Render(Scenario.Required, p => p.Add(x => x.Name, "username"));
        host.Module.Control.Native = Native.ValueMissing;
        Assert.Empty(cut.FindAll("[data-testid=error]"));

        host.Module.FormElement.SubmitAll(cut);

        Assert.Empty(cut.Instance.FormSubmits);
        Assert.Single(cut.FindAll("[data-testid=error]"));
    }

    [Fact(DisplayName = "does not submit when invalid prop remains true even if validate returns null")]
    [Trait("Requirement", "FRM-FRM-01")]
    [Trait("Requirement", "FFI-ALG-14")]
    [Trait("Requirement", "FFI-FRM-27")]
    public void Does_not_submit_when_invalid_prop_remains_true_even_if_validate_returns_null()
    {
        int calls = 0;
        IRenderedComponent<FormErrorScenarios> cut = Render(Scenario.Single, p => p
            .Add(x => x.Name, "name")
            .Add(x => x.Invalid, true)
            .Add(x => x.Mode, ValidationMode.OnChange)
            .Add(x => x.Validate, _ =>
            {
                calls++;
                return new ValueTask<FieldValidationResult?>((FieldValidationResult?)null);
            }));

        host.Module.Control.Focused(cut);
        host.Module.Control.Input(cut, "o");
        Assert.Equal(1, calls);

        Assert.Equal("blocked", host.Module.FormElement.SubmitAll(cut).Verdict);

        Assert.Empty(cut.Instance.Submits);
        Assert.Equal("true", cut.Find("[data-testid=control]").GetAttribute("aria-invalid"));
    }

    private static void Resolve(IRenderedComponent<FormErrorScenarios> cut, List<(object? Value, TaskCompletionSource<FieldValidationResult?> Result)> pending, string value, string? error)
    {
        foreach ((object? Value, TaskCompletionSource<FieldValidationResult?> Result) entry in pending.Where(entry => Equals(entry.Value, value)).ToList())
        {
            cut.InvokeAsync(() => entry.Result.TrySetResult(error));
        }
    }

    private IRenderedComponent<FormErrorScenarios> Render(Scenario scenario, Action<ComponentParameterCollectionBuilder<FormErrorScenarios>>? parameters = null) =>
        Context.Render<FormErrorScenarios>(p =>
        {
            p.Add(x => x.Scenario, scenario);
            parameters?.Invoke(p);
        });
}
