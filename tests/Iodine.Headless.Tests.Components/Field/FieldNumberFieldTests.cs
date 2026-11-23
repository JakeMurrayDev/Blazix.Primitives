using Bunit;
using Scenario = Iodine.Headless.Tests.Components.FieldNumberFieldScenarios.NumberFieldScenario;

namespace Iodine.Headless.Tests.Components;

// The NumberField clauses of FieldRoot.test.tsx's titles, open to M6 by field.md's notes of 2026-11-18 (ADR 12 Amendments
// 2026-11-18, the M6 plan's decision 25 (a); ADR 13 Amendments 2026-10-22); M6 track 6, issue 277, PR 6a. The
// "receives all form values" title's Checkbox, CheckboxGroup, Radio, Select and Switch clauses are their families'; its
// two Slider clauses are open to M6 track 4.
/// <summary>The Field titles that render NumberField.</summary>
public sealed class FieldNumberFieldTests : IDisposable
{
    private const string InteractionsModulePath = "./_content/Iodine.Headless/js/interactions.js";

    private readonly FieldTestContext host = new();

    /// <summary>Initializes a new instance of the <see cref="FieldNumberFieldTests"/> class.</summary>
    public FieldNumberFieldTests() =>
        Context.JSInterop.SetupModule(InteractionsModulePath)
            .Setup<string>("readPlatformFlags")
            .SetResult("""{"voiceOver":false,"mac":false,"ios":false}""");

    private BunitContext Context => host.Context;

    /// <inheritdoc />
    public void Dispose() => host.Dispose();

    [Fact(DisplayName = "receives all form values as the 2nd argument: the NumberField clause")]
    [Trait("Requirement", "FLD-API-04")]
    [Trait("Requirement", "FLD-EVT-08")]
    [Trait("Requirement", "FLD-FRM-02")]
    [Trait("Requirement", "FLD-STR-04")]
    public void Receives_all_form_values_as_the_2nd_argument()
    {
        IRenderedComponent<FieldNumberFieldScenarios> cut = Render(Scenario.AllValues);

        host.Module.FormElement.SubmitAll(cut);

        FieldValidationArguments arguments = Assert.Single(cut.Instance.Validations);
        Assert.Equal("https://base-ui.com", arguments.Value);
        Assert.Equal(["input", "number-field"], arguments.Values.Keys);
        Assert.Equal("https://base-ui.com", arguments.Values["input"]);
        Assert.Equal(13d, arguments.Values["number-field"]);
        Assert.Equal(1, cut.Instance.Submits);
    }

    [Fact(DisplayName = "updates field-aware control name fallbacks when the name changes")]
    [Trait("Requirement", "FLD-FRM-15")]
    [Trait("Requirement", "FFI-STA-10")]
    public void Updates_field_aware_control_name_fallbacks_when_the_name_changes()
    {
        IRenderedComponent<FieldNumberFieldScenarios> cut = Render(Scenario.NameFallback, p => p.Add(x => x.Name, "quantity"));

        host.Module.FormElement.SubmitAll(cut);
        Assert.Equal(["quantity"], cut.Instance.FormSubmits[^1].Values.Keys);
        Assert.Equal(13d, cut.Instance.FormSubmits[^1].Values["quantity"]);

        cut.Render(p => p.Add(x => x.Name, "amount"));
        host.Module.FormElement.SubmitAll(cut);
        Assert.Equal(["amount"], cut.Instance.FormSubmits[^1].Values.Keys);
        Assert.Equal(13d, cut.Instance.FormSubmits[^1].Values["amount"]);

        cut.Render(p => p.Add(x => x.Name, null));
        host.Module.FormElement.SubmitAll(cut);
        Assert.Empty(cut.Instance.FormSubmits[^1].Values);
    }

    [Fact(DisplayName = "clears dirty after an empty text control returns to empty following a null-valued control")]
    [Trait("Requirement", "FLD-STA-14")]
    [Trait("Requirement", "FLD-FRM-14")]
    [Trait("Requirement", "FFI-STA-04")]
    public void Clears_dirty_after_an_empty_text_control_returns_to_empty_following_a_null_valued_control()
    {
        IRenderedComponent<FieldNumberFieldScenarios> cut = Render(Scenario.Swap);

        cut.Render(p => p.Add(x => x.Swapped, true));
        Assert.False(cut.Find("[data-testid=root]").HasAttribute("data-dirty"));

        host.Module.Control.Input(cut, "x");
        Assert.True(cut.Find("[data-testid=root]").HasAttribute("data-dirty"));

        host.Module.Control.Input(cut, string.Empty);
        Assert.False(cut.Find("[data-testid=root]").HasAttribute("data-dirty"));
    }

    private IRenderedComponent<FieldNumberFieldScenarios> Render(Scenario scenario, Action<ComponentParameterCollectionBuilder<FieldNumberFieldScenarios>>? parameters = null) =>
        Context.Render<FieldNumberFieldScenarios>(p =>
        {
            p.Add(x => x.Scenario, scenario);
            parameters?.Invoke(p);
        });
}
