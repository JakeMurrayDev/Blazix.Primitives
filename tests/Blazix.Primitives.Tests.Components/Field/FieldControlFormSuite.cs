using Bunit;
using Microsoft.AspNetCore.Components;
using Scenario = Blazix.Primitives.Tests.Components.FieldFormScenarios.FormScenario;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// <c>FieldControl.test.tsx</c>'s <c>[L2]</c> titles that render <c>Form</c> or <c>Field.Error</c> (PR 1c), run over a
/// control part: <see cref="FieldControl"/> and <see cref="Input"/> derive the same suite (INP-STR-04). The module's reports
/// are driven by <see cref="FieldTestContext.Module"/>; the browser's implicit submission is L4 (field.spec.ts).
/// </summary>
/// <typeparam name="TControl">The type of the control part.</typeparam>
public abstract class FieldControlFormSuite<TControl> : IDisposable
    where TControl : class, IComponent
{
    private readonly FieldTestContext host = new();

    private BunitContext Context => host.Context;

    /// <inheritdoc />
    public void Dispose()
    {
        host.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact(DisplayName = "does not clear errors or validate when change is prevented")]
    [Trait("Requirement", "FLD-API-17")]
    [Trait("Requirement", "FLD-EVT-02")]
    [Trait("Requirement", "FLD-EVT-05")]
    [Trait("Requirement", "FLD-FRM-09")]
    [Trait("Requirement", "FFI-EVT-02")]
    public void Does_not_clear_errors_or_validate_when_change_is_prevented()
    {
        int calls = 0;
        IRenderedComponent<FieldFormScenarios> cut = Render(Scenario.PreventedChange, p => p
            .Add(x => x.Errors, new FormErrors { ["message"] = "Server error" })
            .Add(x => x.ValidationMode, ValidationMode.OnChange)
            .Add(x => x.Validate, _ =>
            {
                calls++;
                return new ValueTask<FieldValidationResult?>((FieldValidationResult?)null);
            }));

        host.Module.Control.Input(cut, "a", defaultPrevented: true);

        Assert.Equal(1, cut.Instance.ValueChanges);
        Assert.Equal(0, calls);
        Assert.Equal("Server error", cut.Find("div > div").TextContent);
    }

    [Fact(DisplayName = "validates the final controlled value when it is normalized on blur")]
    [Trait("Requirement", "FLD-FRM-17")]
    [Trait("Requirement", "FLD-EVT-09")]
    [Trait("Requirement", "FFI-FRM-32")]
    public void Validates_the_final_controlled_value_when_it_is_normalized_on_blur()
    {
        List<object?> validated = [];
        IRenderedComponent<FieldFormScenarios> cut = Render(Scenario.Normalized, p => p
            .Add(x => x.InitialValue, string.Empty)
            .Add(x => x.ValidationMode, ValidationMode.OnBlur)
            .Add(x => x.Validate, arguments =>
            {
                validated.Add(arguments.Value);
                return new ValueTask<FieldValidationResult?>(((string?)arguments.Value)?.Contains('@', StringComparison.Ordinal) == true ? null : "Invalid email");
            }));

        host.Module.Control.Input(cut, "foo ");
        host.Module.Control.Blur(cut);

        Assert.Equal("foo", validated[^1]);
        Assert.Equal("Invalid email", cut.Find("div > div").TextContent);
    }

    // The browser's implicit submission is L4; here the control's half: a keydown Enter in its own form commits nothing,
    // and the submission validates once.
    [Fact(DisplayName = "validates once when Enter implicitly submits a form")]
    [Trait("Requirement", "FLD-KEY-01")]
    [Trait("Requirement", "FFI-ALG-16")]
    [Trait("Requirement", "FFI-KEY-01")]
    public void Validates_once_when_Enter_implicitly_submits_a_form()
    {
        int calls = 0;
        IRenderedComponent<FieldFormScenarios> cut = Render(Scenario.EnterInForm, p => p.Add(x => x.Validate, _ =>
        {
            calls++;
            return new ValueTask<FieldValidationResult?>((FieldValidationResult?)null);
        }));

        host.Module.Control.KeyDown(cut, "Enter", sameForm: true);
        host.Module.FormElement.SubmitAll(cut);

        Assert.Equal(1, calls);
        Assert.Single(cut.Instance.Submits);
    }

    [Fact(DisplayName = "validates the latest value when Enter does not submit the form")]
    [Trait("Requirement", "FLD-KEY-01")]
    [Trait("Requirement", "FLD-KEY-04")]
    [Trait("Requirement", "FFI-ALG-16")]
    [Trait("Requirement", "FFI-KEY-01")]
    public void Validates_the_latest_value_when_Enter_does_not_submit_the_form()
    {
        List<object?> validated = [];
        IRenderedComponent<FieldFormScenarios> cut = Render(Scenario.EnterInForm, p => p
            .Add(x => x.ClearOnKeyDown, true)
            .Add(x => x.InitialValue, "a")
            .Add(x => x.Validate, arguments =>
            {
                validated.Add(arguments.Value);
                return new ValueTask<FieldValidationResult?>((FieldValidationResult?)null);
            }));
        FormModuleStandIn.Attached control = host.Module.Control;

        control.Focused(cut);
        control.KeyDown(cut, "Enter", sameForm: true);
        cut.Find("form").KeyDown(key: "Enter");
        control.Settle(cut);
        control.Enter(cut);

        Assert.Equal([string.Empty], validated);
    }

    private IRenderedComponent<FieldFormScenarios> Render(Scenario scenario, Action<ComponentParameterCollectionBuilder<FieldFormScenarios>>? parameters = null) =>
        Context.Render<FieldFormScenarios>(p =>
        {
            p.Add(x => x.Scenario, scenario);
            p.Add(x => x.ControlType, typeof(TControl));
            parameters?.Invoke(p);
        });
}

/// <summary>The Form titles of <c>FieldControl.test.tsx</c> over <see cref="FieldControl"/>.</summary>
public sealed class FieldControlFormTests : FieldControlFormSuite<FieldControl>;

/// <summary>The Form titles of <c>FieldControl.test.tsx</c> over <see cref="Input"/>.</summary>
public sealed class InputFormTests : FieldControlFormSuite<Input>;
