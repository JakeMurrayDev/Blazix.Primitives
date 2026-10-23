using Bunit;
using Microsoft.AspNetCore.Components;
using Scenario = Blazix.Primitives.Tests.Components.FormScenarios.FormScenario;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The <c>[L2]</c> titles of <c>Form.test.tsx</c> that need no submission and no <c>Field.Error</c>, and port titles for the
/// form scope as fields see it: the error map through <c>data-invalid</c> and <c>aria-invalid</c>, <c>ClearErrors</c> and
/// <c>ValidateAsync</c> reached by <c>@ref</c>.
/// </summary>
public sealed class FormTests : IDisposable
{
    private readonly FieldTestContext host = new();

    private BunitContext Context => host.Context;

    /// <inheritdoc />
    public void Dispose() => host.Dispose();

    [Fact(DisplayName = "should disable native validation if set to true (default)")]
    [Trait("Requirement", "FRM-API-06")]
    [Trait("Requirement", "FRM-DOM-01")]
    [Trait("Requirement", "FRM-STR-02")]
    public void Should_disable_native_validation_by_default()
    {
        IRenderedComponent<Form> cut = Context.Render<Form>(p => p.AddUnmatched("data-testid", "form"));

        Assert.True(cut.Find("[data-testid=form]").HasAttribute("novalidate"));
    }

    [Fact(DisplayName = "should enable native validation if set to false")]
    [Trait("Requirement", "FRM-API-06")]
    [Trait("Requirement", "FRM-DOM-01")]
    public void Should_enable_native_validation_if_set_to_false()
    {
        IRenderedComponent<Form> cut = Context.Render<Form>(p => p.Add(x => x.NoValidate, false).AddUnmatched("data-testid", "form"));

        Assert.False(cut.Find("[data-testid=form]").HasAttribute("novalidate"));
    }

    // ADR 06 Amendments 2026-10-18 rule 2: the StrictMode replay does not arise; the title's rule is the current registry.
    [Fact(DisplayName = "targets only the current Strict Mode registration after name, id, and control replacement")]
    [Trait("Requirement", "FRM-API-05")]
    [Trait("Requirement", "FRM-FRM-12")]
    [Trait("Requirement", "FRM-EDGE-02")]
    public void Targets_only_the_current_registration_after_name_id_and_control_replacement()
    {
        List<string> calls = [];
        IRenderedComponent<FormScenarios> cut = Context.Render<FormScenarios>(p => p.Add(x => x.Scenario, Scenario.Replacement).Add(x => x.Calls, calls));

        cut.Render(p => p.Add(x => x.Step, 1));
        cut.InvokeAsync(() => cut.Instance.Form.ValidateAsync("initial"));
        cut.InvokeAsync(() => cut.Instance.Form.ValidateAsync("current"));
        Assert.Equal(["renamed"], calls);

        cut.Render(p => p.Add(x => x.Step, 2));
        cut.InvokeAsync(() => cut.Instance.Form.ValidateAsync("current"));
        Assert.Equal(["renamed"], calls);

        cut.Render(p => p.Add(x => x.Step, 3));
        cut.InvokeAsync(() => cut.Instance.Form.ValidateAsync("current"));
        Assert.Equal(["renamed", "replacement"], calls);
    }

    [Fact(DisplayName = "validates every registered field in registration order, or only the first with a name")]
    [Trait("Requirement", "FRM-API-05")]
    [Trait("Requirement", "FRM-FRM-11")]
    [Trait("Requirement", "FRM-EDGE-01")]
    [Trait("Requirement", "FRM-EDGE-02")]
    public void Validates_every_registered_field_or_only_the_first_with_a_name()
    {
        List<string> calls = [];
        IRenderedComponent<FormScenarios> cut = Context.Render<FormScenarios>(p => p.Add(x => x.Scenario, Scenario.Validation).Add(x => x.Calls, calls));

        cut.InvokeAsync(() => cut.Instance.Form.ValidateAsync());
        Assert.Equal(["first", "second", "second-again"], calls);

        calls.Clear();
        cut.InvokeAsync(() => cut.Instance.Form.ValidateAsync(string.Empty));
        Assert.Equal(["first", "second", "second-again"], calls);

        calls.Clear();
        cut.InvokeAsync(() => cut.Instance.Form.ValidateAsync("second"));
        Assert.Equal(["second"], calls);

        calls.Clear();
        cut.InvokeAsync(() => cut.Instance.Form.ValidateAsync("absent"));
        Assert.Empty(calls);
    }

    // Issue 78's binding check: Blazor binds component parameters case-insensitively, so Razor's `@onsubmit` directive on
    // Form sets the OnSubmit parameter itself, with an EventCallback<EventArgs> it cannot take. Nothing named onsubmit can
    // reach Form's forwarded attributes, so no second Blazor handler runs for the submit event the form module owns.
    [Fact(DisplayName = "a Razor @onsubmit on Form binds to OnSubmit and fails, so OnSubmit is the only submit handler")]
    [Trait("Requirement", "FRM-API-04")]
    public void A_Razor_onsubmit_directive_binds_to_OnSubmit_and_fails()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Context.Render<FormOnSubmitBinding>());

        Assert.Contains("Unable to set property 'onsubmit' on object of type 'Blazix.Primitives.Form'", error.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "marks the field named in Errors invalid, and a new instance replaces the errors")]
    [Trait("Requirement", "FRM-API-02")]
    [Trait("Requirement", "FRM-FRM-05")]
    [Trait("Requirement", "FRM-STA-02")]
    [Trait("Requirement", "FRM-EDGE-03")]
    public void Marks_the_field_named_in_Errors_invalid()
    {
        IRenderedComponent<FormScenarios> cut = Context.Render<FormScenarios>(p => p
            .Add(x => x.Scenario, Scenario.Errors)
            .Add(x => x.Errors, new FormErrors { ["email"] = "Email is taken" }));

        Assert.Equal("true", cut.Find("[data-testid=email]").GetAttribute("aria-invalid"));
        Assert.True(cut.Find("[data-testid=email-root]").HasAttribute("data-invalid"));
        Assert.False(cut.Find("[data-testid=name]").HasAttribute("aria-invalid"));

        cut.Render(p => p.Add(x => x.Errors, new FormErrors { ["name"] = new[] { "Name is taken", "Name is too short" } }));
        Assert.False(cut.Find("[data-testid=email]").HasAttribute("aria-invalid"));
        Assert.Equal("true", cut.Find("[data-testid=name]").GetAttribute("aria-invalid"));

        cut.Render(p => p.Add(x => x.Errors, (FormErrors?)null));
        Assert.False(cut.Find("[data-testid=name]").HasAttribute("aria-invalid"));
    }

    [Fact(DisplayName = "removes exactly one error through ClearErrors, which stays removed until a new instance")]
    [Trait("Requirement", "FRM-EVT-06")]
    [Trait("Requirement", "FFI-EVT-04")]
    public void Removes_exactly_one_error_through_ClearErrors()
    {
        FormErrors errors = new() { ["email"] = "Email is taken", ["name"] = "Name is taken" };
        IRenderedComponent<FormScenarios> cut = Context.Render<FormScenarios>(p => p.Add(x => x.Scenario, Scenario.Errors).Add(x => x.Errors, errors));

        cut.InvokeAsync(() => cut.Instance.Form.ClearErrors("email"));

        Assert.False(cut.Find("[data-testid=email]").HasAttribute("aria-invalid"));
        Assert.Equal("true", cut.Find("[data-testid=name]").GetAttribute("aria-invalid"));

        cut.Render(p => p.Add(x => x.Errors, errors));
        Assert.False(cut.Find("[data-testid=email]").HasAttribute("aria-invalid"));

        cut.Render(p => p.Add(x => x.Errors, new FormErrors { ["email"] = "Email is taken" }));
        Assert.Equal("true", cut.Find("[data-testid=email]").GetAttribute("aria-invalid"));
    }

    [Fact(DisplayName = "a field's change clears its own form error and leaves the others")]
    [Trait("Requirement", "FRM-FRM-06")]
    [Trait("Requirement", "FRM-EVT-06")]
    [Trait("Requirement", "FFI-EVT-02")]
    public void A_fields_change_clears_its_own_form_error()
    {
        IRenderedComponent<FormScenarios> cut = Context.Render<FormScenarios>(p => p
            .Add(x => x.Scenario, Scenario.Errors)
            .Add(x => x.Errors, new FormErrors { ["email"] = "Email is taken", ["name"] = "Name is taken" }));
        // The controls attach in document order: the email control first.
        host.Module.Controls[0].Input(cut, "new@example.com");

        Assert.False(cut.Find("[data-testid=email]").HasAttribute("aria-invalid"));
        Assert.Equal("true", cut.Find("[data-testid=name]").GetAttribute("aria-invalid"));
    }
}
