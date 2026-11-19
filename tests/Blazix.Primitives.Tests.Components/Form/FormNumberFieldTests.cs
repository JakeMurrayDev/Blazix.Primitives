using Blazix.Primitives.Interop;
using Bunit;
using Scenario = Blazix.Primitives.Tests.Components.FormNumberFieldScenarios.NumberFieldScenario;

namespace Blazix.Primitives.Tests.Components;

// FRM-API-03, FRM-API-05, FRM-EVT-03, FRM-EVT-04 and FRM-FRM-11: the NumberField halves of Form.test.tsx's titles, open to
// M6 by form.md's port notes of 2026-10-23 (ADR 13 Amendments 2026-10-22); M6 track 6, issue 277, PR 6a. A native submission
// is the stand-in module's report of every enrolled control; the number field's value is its registry entry's typed value.
/// <summary>The Form titles that render NumberField.</summary>
public sealed class FormNumberFieldTests : IDisposable
{
    private const string InteractionsModulePath = "./_content/Blazix.Primitives/js/interactions.js";

    private readonly FieldTestContext host = new();

    /// <summary>Initializes a new instance of the <see cref="FormNumberFieldTests"/> class.</summary>
    public FormNumberFieldTests() =>
        Context.JSInterop.SetupModule(InteractionsModulePath)
            .Setup<string>("readPlatformFlags")
            .SetResult("""{"voiceOver":false,"mac":false,"ios":false}""");

    private BunitContext Context => host.Context;

    /// <inheritdoc />
    public void Dispose() => host.Dispose();

    [Fact(DisplayName = "runs when the form is submitted")]
    [Trait("Requirement", "FRM-API-03")]
    [Trait("Requirement", "FRM-EVT-01")]
    [Trait("Requirement", "FRM-EVT-03")]
    [Trait("Requirement", "FRM-EVT-04")]
    public void Runs_when_the_form_is_submitted()
    {
        IRenderedComponent<FormNumberFieldScenarios> cut = Render(Scenario.Values);

        FormSubmitAnswer answer = host.Module.FormElement.SubmitAll(cut);

        FormSubmitEventArgs args = Assert.Single(cut.Instance.FormSubmits);
        Assert.Equal(["username", "quantity"], args.Values.Keys);
        Assert.Equal("alice132", args.Values["username"]);
        Assert.Equal(5d, args.Values["quantity"]);
        Assert.True(args.Values.TryGetValue("quantity", out double? quantity));
        Assert.Equal(5, quantity);
        Assert.True(args.Details.Event.DefaultPrevented);
        Assert.Equal(EventReason.None, args.Details.Reason);
        Assert.Equal("handled", answer.Verdict);
    }

    [Fact(DisplayName = "validates the form when the `validate` method is called")]
    [Trait("Requirement", "FRM-API-05")]
    [Trait("Requirement", "FRM-FRM-11")]
    public async Task Validates_the_form_when_the_validate_method_is_called()
    {
        IRenderedComponent<FormNumberFieldScenarios> cut = Render(Scenario.Validate);
        Username().Blur(cut, Native.ValueMissing);
        Assert.Empty(cut.FindAll("[data-testid=error]"));

        await Validate(cut, null);

        Assert.Equal(2, cut.FindAll("[data-testid=error]").Count);
        Assert.Empty(cut.Instance.FormSubmits);
    }

    [Fact(DisplayName = "validates a field when the `validate` method is called with the field name")]
    [Trait("Requirement", "FRM-API-05")]
    [Trait("Requirement", "FRM-FRM-11")]
    public async Task Validates_a_field_when_the_validate_method_is_called_with_the_field_name()
    {
        IRenderedComponent<FormNumberFieldScenarios> cut = Render(Scenario.Validate, "number field error");
        Username().Blur(cut, Native.ValueMissing);
        Assert.Empty(cut.FindAll("[data-testid=error]"));

        await Validate(cut, "quantity");

        Assert.Equal("number field error", Assert.Single(cut.FindAll("[data-testid=error]")).TextContent);
    }

    // The required username is empty: the module reports its native state with the blur, which in the default onSubmit mode
    // shows nothing (Base UI reads the DOM's validity at validate time). The username control is the one whose kept value is empty; the number field's hidden input keeps "5".
    private FormModuleStandIn.Attached Username() => host.Module.Controls.Single(control => control.DomValue.Length == 0);

    private IRenderedComponent<FormNumberFieldScenarios> Render(Scenario scenario, string quantityError = "error") =>
        Context.Render<FormNumberFieldScenarios>(p => p.Add(x => x.Scenario, scenario).Add(x => x.QuantityError, quantityError));

    // Base UI's button calls actionsRef.validate(); the port's ValidateAsync first asks each control for a fresh report,
    // which the stand-in module answers (FieldRootFormTests' pattern).
    private async Task Validate(IRenderedComponent<FormNumberFieldScenarios> cut, string? name)
    {
        Task validation = cut.InvokeAsync(() => cut.Instance.Form.ValidateAsync(name));
        host.Module.Settle(cut);
        await validation;
    }
}
