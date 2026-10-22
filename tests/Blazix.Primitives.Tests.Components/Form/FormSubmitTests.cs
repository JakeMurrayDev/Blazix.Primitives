using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Scenario = Blazix.Primitives.Tests.Components.FormSubmitScenarios.SubmitScenario;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The submission pipeline at L2 (FRM-EVT-01, FFI-ALG-05): the <c>Form.test.tsx</c> titles that render only Form, Fieldset
/// and Field.Control (issue 78's landing rule), and the twin contract of the submit half. A native submission is the
/// stand-in module's report: the submitter and the state it reads from every enrolled control, answered by C#.
/// </summary>
public sealed class FormSubmitTests : IDisposable
{
    private readonly FieldTestContext host = new();

    private BunitContext Context => host.Context;

    /// <inheritdoc />
    public void Dispose() => host.Dispose();

    [Fact(DisplayName = "submits when a valid async validator is pending")]
    [Trait("Requirement", "FRM-FRM-02")]
    [Trait("Requirement", "FRM-EVT-02")]
    [Trait("Requirement", "FRM-API-04")]
    public void Submits_when_a_valid_async_validator_is_pending()
    {
        IRenderedComponent<FormSubmitScenarios> cut = Render(Scenario.Pending);

        FormSubmitAnswer answer = Submit(cut, Read(cut, "control", string.Empty));

        Assert.Equal(1, cut.Instance.PendingCalls);
        Assert.Single(cut.Instance.Submits);
        Assert.Equal("submit", answer.Verdict);
    }

    [Fact(DisplayName = "unmounted fields should be removed from the form")]
    [Trait("Requirement", "FRM-FRM-04")]
    [Trait("Requirement", "FRM-STA-05")]
    [Trait("Requirement", "FRM-EVT-02")]
    public void Unmounted_fields_should_be_removed_from_the_form()
    {
        IRenderedComponent<FormSubmitScenarios> cut = Render(Scenario.Unmount);

        Submit(cut, Read(cut, "name", "Alice"), Read(cut, "email", string.Empty, Native.ValueMissing));
        Assert.Empty(cut.Instance.Submits);
        Assert.Equal("true", cut.Find("[data-testid=email]").GetAttribute("aria-invalid"));

        cut.Render(p => p.Add(x => x.Checked, false));
        Submit(cut, Read(cut, "name", "Alice"));
        Assert.Single(cut.Instance.Submits);
    }

    [Fact(DisplayName = "excludes disabled fieldset fields from validation and onFormSubmit values")]
    [Trait("Requirement", "FRM-FRM-04")]
    [Trait("Requirement", "FRM-EVT-04")]
    [Trait("Requirement", "FST-FRM-01")]
    public void Excludes_disabled_fieldset_fields_from_validation_and_values()
    {
        IRenderedComponent<FormSubmitScenarios> cut = Render(Scenario.Fieldset, p => p.Add(x => x.Disabled, true));

        Submit(cut, Read(cut, "disabled", string.Empty, Native.ValueMissing), Read(cut, "enabled", "sent"));

        FormSubmitEventArgs args = Assert.Single(cut.Instance.FormSubmits);
        Assert.Equal(["enabled"], args.Values.Keys);
        Assert.Equal("sent", args.Values["enabled"]);
        Assert.False(cut.Find("[data-testid=disabled]").HasAttribute("aria-invalid"));
    }

    [Fact(DisplayName = "clears invalid attributes when a field control becomes disabled")]
    [Trait("Requirement", "FRM-FRM-04")]
    [Trait("Requirement", "FRM-EVT-04")]
    [Trait("Requirement", "FRM-EDGE-04")]
    public void Clears_invalid_attributes_when_a_field_control_becomes_disabled()
    {
        IRenderedComponent<FormSubmitScenarios> cut = Render(Scenario.ControlDisabled);

        Submit(cut, Read(cut, "control", string.Empty, Native.ValueMissing));
        Assert.Equal("true", cut.Find("[data-testid=control]").GetAttribute("aria-invalid"));

        cut.Render(p => p.Add(x => x.Disabled, true));
        Assert.True(cut.Find("[data-testid=control]").HasAttribute("disabled"));
        Assert.False(cut.Find("[data-testid=control]").HasAttribute("aria-invalid"));

        Submit(cut, Read(cut, "control", string.Empty, Native.ValueMissing));
        FormSubmitEventArgs args = Assert.Single(cut.Instance.FormSubmits);
        Assert.Empty(args.Values);
    }

    [Fact(DisplayName = "re-registers field controls when they become enabled again")]
    [Trait("Requirement", "FRM-FRM-04")]
    [Trait("Requirement", "FRM-EVT-04")]
    public void Re_registers_field_controls_when_they_become_enabled_again()
    {
        IRenderedComponent<FormSubmitScenarios> cut = Render(Scenario.ControlDisabled);

        Submit(cut, Read(cut, "control", string.Empty, Native.ValueMissing));
        Assert.Empty(cut.Instance.FormSubmits);
        Assert.Equal("true", cut.Find("[data-testid=control]").GetAttribute("aria-invalid"));

        cut.Render(p => p.Add(x => x.Disabled, true));
        Submit(cut, Read(cut, "control", string.Empty, Native.ValueMissing));
        Assert.Empty(Assert.Single(cut.Instance.FormSubmits).Values);

        cut.Render(p => p.Add(x => x.Disabled, false));
        Submit(cut, Read(cut, "control", string.Empty, Native.ValueMissing));
        Assert.Single(cut.Instance.FormSubmits);
        Assert.Equal("true", cut.Find("[data-testid=control]").GetAttribute("aria-invalid"));

        host.Module.Control.Input(cut, "sent", Native.Valid);
        Submit(cut, Read(cut, "control", "sent"));
        Assert.Equal(2, cut.Instance.FormSubmits.Count);
        Assert.Equal("sent", cut.Instance.FormSubmits[^1].Values["control"]);
    }

    // The focus itself is the module's (L3, L4); here the answer names the invalid controls in registration order.
    [Fact(DisplayName = "blocks submit and focuses the first invalid field across custom and native validation")]
    [Trait("Requirement", "FRM-FRM-01")]
    [Trait("Requirement", "FRM-EVT-05")]
    [Trait("Requirement", "FRM-FOC-01")]
    public void Blocks_submit_across_custom_and_native_validation()
    {
        IRenderedComponent<FormSubmitScenarios> cut = Render(Scenario.CustomAndNative);

        FormSubmitAnswer answer = Submit(cut, Read(cut, "custom", string.Empty), Read(cut, "native", string.Empty, Native.ValueMissing));

        Assert.Empty(cut.Instance.FormSubmits);
        Assert.Equal("blocked", answer.Verdict);
        Assert.Equal([IdOf(cut, "custom"), IdOf(cut, "native")], answer.Focus);
    }

    // FRM-API-03 and FRM-EVT-03 re-cited (ADR 13 Amendments 2026-10-22): Base UI's title adds a NumberField (`quantity: 5`),
    // which arrives in M6; the port's title keeps its Field.Control half and adds an unnamed field, which submits nothing.
    [Fact(DisplayName = "runs OnFormSubmit with the named fields' values and a prevented submit event")]
    [Trait("Requirement", "FRM-API-03")]
    [Trait("Requirement", "FRM-EVT-01")]
    [Trait("Requirement", "FRM-EVT-03")]
    [Trait("Requirement", "FRM-EVT-04")]
    public void Runs_OnFormSubmit_with_the_named_fields_values()
    {
        IRenderedComponent<FormSubmitScenarios> cut = Render(Scenario.Values);

        FormSubmitAnswer answer = Submit(cut, Read(cut, "username", "alice132"), Read(cut, "unnamed", "unnamed"));

        FormSubmitEventArgs args = Assert.Single(cut.Instance.FormSubmits);
        Assert.Equal(["username"], args.Values.Keys);
        Assert.Equal("alice132", args.Values["username"]);
        Assert.Equal(EventReason.None, args.Details.Reason);
        Assert.True(args.Details.Event.DefaultPrevented);
        Assert.Equal("submit", args.Details.Event.Type);
        Assert.Equal("handled", answer.Verdict);
    }

    [Fact(DisplayName = "attaches the form module to the form element with the submit options")]
    [Trait("Verifies", "ADR06-3")]
    [Trait("Requirement", "FRM-EVT-01")]
    public void Attaches_the_form_module_to_the_form_element()
    {
        Render(Scenario.Values);

        FormSubmitOptions options = Assert.IsType<FormSubmitOptions>(host.Module.FormElement.SubmitOptions);
        Assert.Equal(0, options.FocusRevision);
    }

    [Fact(DisplayName = "answers submit, handled or blocked, and counts every submission")]
    [Trait("Verifies", "ADR06-3")]
    [Trait("Requirement", "FRM-EVT-01")]
    [Trait("Requirement", "FRM-EVT-02")]
    [Trait("Requirement", "FRM-STA-03")]
    public void Answers_submit_handled_or_blocked_and_counts_every_submission()
    {
        List<string> order = [];
        IRenderedComponent<FormSubmitScenarios> cut = Render(Scenario.Callbacks, p => p
            .Add(x => x.OnSubmit, EventCallback.Factory.Create<FormSubmitEventArgs>(this, _ => order.Add("submit")))
            .Add(x => x.Validator, args => new((string?)args.Value == "bad" ? "error" : null)));

        Assert.Equal("submit", Submit(cut, Read(cut, "email", "good")).Verdict);
        Assert.Equal(["submit"], order);

        Assert.Equal("blocked", Submit(cut, Read(cut, "email", "bad")).Verdict);
        Assert.Equal(["submit"], order);
        Assert.Equal(2, cut.Instance.Form.SubmitCount);

        cut.Render(p => p.Add(x => x.OnFormSubmit, EventCallback.Factory.Create<FormSubmitEventArgs>(this, _ => order.Add("form submit"))));
        Assert.Equal("handled", Submit(cut, Read(cut, "email", "good")).Verdict);
        Assert.Equal(["submit", "submit", "form submit"], order);
        Assert.Equal(3, cut.Instance.Form.SubmitCount);
    }

    [Fact(DisplayName = "does not re-issue the submission when OnSubmit prevents it")]
    [Trait("Requirement", "FRM-EVT-02")]
    [Trait("Requirement", "FRM-API-04")]
    public void Does_not_re_issue_the_submission_when_OnSubmit_prevents_it()
    {
        IRenderedComponent<FormSubmitScenarios> cut = Render(Scenario.Callbacks, p => p
            .Add(x => x.OnSubmit, EventCallback.Factory.Create<FormSubmitEventArgs>(this, args => args.PreventNativeSubmit())));

        Assert.Equal("handled", Submit(cut, Read(cut, "email", "good")).Verdict);
    }

    // Design point F2 (raised with the orchestrator): the module reads every enrolled control in the submit task, so a value
    // a script wrote without an event is the one validated (FFI-EDGE-07, FFI-FRM-04).
    [Fact(DisplayName = "validates the value a script wrote without an event when the form submits")]
    [Trait("Requirement", "FFI-FRM-04")]
    [Trait("Requirement", "FFI-EDGE-07")]
    [Trait("Requirement", "FRM-FRM-01")]
    public void Validates_the_value_a_script_wrote_without_an_event()
    {
        List<object?> validated = [];
        IRenderedComponent<FormSubmitScenarios> cut = Render(Scenario.Callbacks, p => p
            .Add(x => x.Validator, args => { validated.Add(args.Value); return new((string?)args.Value == "bad" ? "error" : null); }));

        FormSubmitAnswer answer = Submit(cut, Read(cut, "email", "bad"));

        Assert.Equal(["bad"], validated);
        Assert.Equal("blocked", answer.Verdict);
    }

    [Fact(DisplayName = "asks the module to focus the first invalid field on the first change of the errors after a passed submission")]
    [Trait("Requirement", "FRM-FOC-02")]
    [Trait("Requirement", "FRM-STA-04")]
    [Trait("Requirement", "FFI-FOC-03")]
    public void Asks_the_module_to_focus_after_a_passed_submission()
    {
        IRenderedComponent<FormSubmitScenarios> cut = Render(Scenario.Callbacks);
        Attachment form = new(host);

        // A change of the errors before any passed submission focuses nothing.
        cut.Render(p => p.Add(x => x.Errors, new FormErrors()));
        Assert.Equal(0, form.FocusRevision);

        Assert.Equal("submit", Submit(cut, Read(cut, "email", "a"), Read(cut, "name", "b")).Verdict);
        cut.Render(p => p.Add(x => x.Errors, new FormErrors { ["email"] = "Email is taken" }));
        Assert.Equal(1, form.FocusRevision);
        Assert.Equal([IdOf(cut, "email")], form.Focus);

        cut.Render(p => p.Add(x => x.Errors, new FormErrors { ["name"] = "Name is taken" }));
        Assert.Equal(1, form.FocusRevision);
    }

    private IRenderedComponent<FormSubmitScenarios> Render(Scenario scenario, Action<ComponentParameterCollectionBuilder<FormSubmitScenarios>>? parameters = null) =>
        Context.Render<FormSubmitScenarios>(p =>
        {
            p.Add(x => x.Scenario, scenario);
            parameters?.Invoke(p);
        });

    private FormSubmitAnswer Submit(IRenderedComponent<FormSubmitScenarios> cut, params FormControlState[] controls) =>
        host.Module.FormElement.Submit(cut, null, controls);

    private static string IdOf(IRenderedComponent<FormSubmitScenarios> cut, string testId) => cut.Find($"[data-testid={testId}]").GetAttribute("id")!;

    private static FormControlState Read(IRenderedComponent<FormSubmitScenarios> cut, string testId, string value, NativeValidity? validity = null) =>
        new() { Id = IdOf(cut, testId), Value = value, Validity = validity ?? Native.Valid };

    /// <summary>The form element's latest submit options, as the module last received them.</summary>
    private sealed class Attachment(FieldTestContext host)
    {
        private FormSubmitOptions Latest => host.Module.FormElement.SubmitUpdates() is { Count: > 0 } updates ? updates[^1] : host.Module.FormElement.SubmitOptions!;

        public int FocusRevision => Latest.FocusRevision;

        public IReadOnlyList<string>? Focus => Latest.Focus;
    }
}
