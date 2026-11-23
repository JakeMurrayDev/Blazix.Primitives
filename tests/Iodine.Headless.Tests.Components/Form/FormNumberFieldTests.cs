using AngleSharp.Dom;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components;
using Scenario = Blazix.Primitives.Tests.Components.FormNumberFieldScenarios.NumberFieldScenario;

namespace Blazix.Primitives.Tests.Components;

// FRM-API-03, FRM-API-05, FRM-EVT-03, FRM-EVT-04 and FRM-FRM-11: the NumberField halves of Form.test.tsx's titles, open to
// M6 by form.md's port notes of 2026-10-23 (ADR 13 Amendments 2026-10-22); M6 track 6, issue 277, PR 6a. A native submission
// is the stand-in module's report of every enrolled control; the number field's value is its registry entry's typed value.
/// <summary>The Form titles that render NumberField.</summary>
public sealed class FormNumberFieldTests : IDisposable
{
    private const string InteractionsModulePath = "./_content/Blazix.Primitives/js/interactions.js";
    private const string ButtonModulePath = "./_content/Blazix.Primitives/js/button.js";

    private readonly FieldTestContext host = new();

    /// <summary>Initializes a new instance of the <see cref="FormNumberFieldTests"/> class.</summary>
    public FormNumberFieldTests()
    {
        Context.JSInterop.SetupModule(InteractionsModulePath)
            .Setup<string>("readPlatformFlags")
            .SetResult("""{"voiceOver":false,"mac":false,"ios":false}""");
        ButtonModule = Context.JSInterop.SetupModule(ButtonModulePath);
        BunitJSModuleInterop handle = ButtonModule.SetupModule("attach", _ => true);
        handle.SetupVoid("update", _ => true).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
    }

    private BunitJSModuleInterop ButtonModule { get; }

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

    // FRM-FOC-01 at L2: C# answers `blocked` with the number field's control id, and the form module focuses it (the focus
    // itself, NUM-FRM-12's "(L4 for the focus-first-invalid clause)").
    [Fact(DisplayName = "focuses the input when the field receives an error from Form")]
    [Trait("Requirement", "NUM-FRM-12")]
    [Trait("Requirement", "NUM-ARIA-02")]
    public void Focuses_the_input_when_the_field_receives_an_error_from_Form()
    {
        IRenderedComponent<FormNumberFieldScenarios> cut = Render(Scenario.ServerError);
        Assert.Empty(cut.FindAll("[data-testid=error]"));

        host.Module.FormElement.SubmitAll(cut);

        IElement input = cut.Find("input[type=text]");
        Assert.Equal("true", input.GetAttribute("aria-invalid"));
        Assert.Equal("server error", cut.Find("[data-testid=error]").TextContent);
        Assert.Contains(input.GetAttribute("id"), host.Module.FormElement.SubmitUpdates()[^1].Focus ?? []);
    }

    [Fact(DisplayName = "revalidates immediately after form submission errors using increment button")]
    [Trait("Requirement", "NUM-FRM-08")]
    [Trait("Requirement", "NUM-ARIA-02")]
    public async Task Revalidates_immediately_after_form_submission_errors_using_increment_button()
    {
        IRenderedComponent<FormNumberFieldScenarios> cut = Render(Scenario.Required);

        // The hidden <input type="number" required> is the field's validation input. It renders no id, so the module reports
        // its native state with each refresh, not in the submission (NUM-DOM-06): empty, it is valueMissing.
        FormModuleStandIn.Attached hidden = Assert.Single(host.Module.Controls);
        hidden.Native = Native.ValueMissing;
        host.Module.Settle(cut);
        host.Module.FormElement.SubmitAll(cut);

        Assert.Equal("required", cut.Find("[data-testid=error]").TextContent);
        IElement input = cut.Find("input[type=text]");
        Assert.Equal("true", input.GetAttribute("aria-invalid"));

        hidden.Native = Native.Valid;
        await ClickIncrementAsync(cut);
        host.Module.Settle(cut);

        // The value reaction revalidates after the hidden input's refresh answer, a continuation that runs after the answer;
        // aria-invalid clears then, and the error leaves after its exit transition, which the stand-in ends.
        cut.WaitForAssertion(() => Assert.False(cut.Find("input[type=text]").HasAttribute("aria-invalid")));
        host.CompleteExits(cut);
        Assert.Empty(cut.FindAll("[data-testid=error]"));
    }

    // The required username is empty: the module reports its native state with the blur, which in the default onSubmit mode
    // shows nothing (Base UI reads the DOM's validity at validate time). The username control is the one whose kept value is empty; the number field's hidden input keeps "5".
    private FormModuleStandIn.Attached Username() => host.Module.Controls.Single(control => control.DomValue.Length == 0);

    // A single-action click on the increment, as the button module reports a keyboard click or a tap.
    private Task ClickIncrementAsync(IRenderedComponent<FormNumberFieldScenarios> cut)
    {
        ElementReference? element = cut.FindComponent<NumberFieldIncrement>().Instance.Element;
        JSRuntimeInvocation attach = ButtonModule.Invocations["attach"].Last(i => Equals(i.Arguments[0], element));
        ButtonTwin twin = ((DotNetObjectReference<ButtonTwin>)attach.Arguments[2]!).Value;
        string json = $$"""{"event":{{NumberFieldTestContext.EventJson("click", "", 0)}}}""";
        return cut.InvokeAsync(() => twin.OnHoldClick(json));
    }

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
