using System.Text.Json;
using AngleSharp.Dom;
using Iodine.Headless.Interop;
using Bunit;
using Microsoft.JSInterop;
using Scenario = Iodine.Headless.Tests.Components.NumberFieldAutofillScenarios.AutofillScenario;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Iodine.Headless.Tests.Components;

// 6b's input role at L2 (issue 277): NumberFieldRoot.test.tsx's titles on the hidden input's autofill and on the onBlur
// validation of typed text. Base UI's fireEvent.change on the hidden input is the `number-field` module's autofill report,
// and its fireEvent.focus, change and blur on the visible input are the module's focus, change and blur reports (ADR 06 §1).
// The module reports every autofill; C# decides whether a disabled or read-only field ignores it. A field's validation reads
// the hidden input's native state from a fresh `form` report first, so the `form` module is the Field tests' stand-in
// (FieldTestContext), which answers the refreshes each report caused.
/// <summary>Autofill of the hidden input and the onBlur validation of a number field.</summary>
public sealed class NumberFieldAutofillTests : IDisposable
{
    private readonly FieldTestContext host = new();
    private readonly BunitJSModuleInterop numberFieldModule;
    private readonly BunitJSModuleInterop numberFieldHandle;
    private int revision;

    /// <summary>Initializes a new instance of the <see cref="NumberFieldAutofillTests"/> class.</summary>
    public NumberFieldAutofillTests()
    {
        host.Context.JSInterop.SetupModule(NumberFieldTestContext.InteractionsModulePath)
            .Setup<string>("readPlatformFlags")
            .SetResult("""{"voiceOver":false,"mac":false,"ios":false}""");
        numberFieldModule = host.Context.JSInterop.SetupModule(NumberFieldTestContext.NumberFieldModulePath);
        numberFieldHandle = numberFieldModule.SetupModule("attach", _ => true);
        numberFieldHandle.SetupVoid("update", _ => true).SetVoidResult();
        numberFieldHandle.SetupVoid("dispose", _ => true).SetVoidResult();
    }

    // The text the visible input shows: the last text sent to the `number-field` module.
    private string Text => JsonDocument.Parse(
        numberFieldHandle.Invocations["update"].Count > 0
            ? (string)numberFieldHandle.Invocations["update"][^1].Arguments[0]!
            : (string)numberFieldModule.Invocations["attach"][^1].Arguments[1]!).RootElement.GetProperty("value").GetString()!;

    /// <inheritdoc />
    public void Dispose() => host.Dispose();

    [Fact(DisplayName = "validates the parsed number when handling browser autofill")]
    [Trait("Requirement", "NUM-FRM-05")]
    public async Task Validates_the_parsed_number_when_handling_browser_autofill()
    {
        IRenderedComponent<NumberFieldAutofillScenarios> cut = Render(Scenario.ValidateOnChange);

        await AutofillAsync(cut, 42);

        Assert.NotEmpty(cut.Instance.Validations);
        Assert.All(cut.Instance.Validations, v => Assert.Equal(42d, v.Value));
        Assert.Equal(42d, cut.Instance.Validations[^1].Value);
    }

    [Theory(DisplayName = "ignores hidden-input autofill when $lockState inside Field")]
    [InlineData("readOnly", "true")]
    [InlineData("disabled", null)]
    [Trait("Requirement", "NUM-FRM-05")]
    [Trait("Requirement", "NUM-ARIA-02")]
    public async Task Ignores_hidden_input_autofill_when_locked_inside_Field(string lockState, string? ariaInvalid)
    {
        IRenderedComponent<NumberFieldAutofillScenarios> cut = Render(Scenario.LockedInsideField, lockState);
        Assert.Equal("test", cut.Find("[data-testid=error]").TextContent);
        // A disabled control is not marked invalid.
        Assert.Equal(ariaInvalid, Input(cut).GetAttribute("aria-invalid"));

        await AutofillAsync(cut, 42);

        Assert.Empty(cut.Instance.Changes);
        Assert.Equal("1", Text);
        Assert.Equal("test", cut.Find("[data-testid=error]").TextContent);
    }

    [Theory(DisplayName = "ignores hidden-input autofill when $lockState outside Field")]
    [InlineData("readOnly")]
    [InlineData("disabled")]
    [Trait("Requirement", "NUM-FRM-05")]
    public async Task Ignores_hidden_input_autofill_when_locked_outside_Field(string lockState)
    {
        IRenderedComponent<NumberFieldAutofillScenarios> cut = Render(Scenario.LockedOutsideField, lockState);
        Assert.Equal("quantity", cut.Find("input[type=number]").GetAttribute("name"));

        await AutofillAsync(cut, 42);

        Assert.Empty(cut.Instance.Changes);
        Assert.Equal("1", Text);
    }

    [Fact(DisplayName = "is validated with latest value when validationMode=onBlur")]
    [Trait("Requirement", "NUM-FRM-09")]
    [Trait("Requirement", "NUM-FRM-06")]
    public async Task Is_validated_with_latest_value_when_validationMode_onBlur()
    {
        IRenderedComponent<NumberFieldAutofillScenarios> cut = Render(Scenario.OnBlurLatest);

        await FocusAsync(cut);
        await ChangeAsync(cut, "1");
        await BlurAsync(cut, "1");

        FieldValidationArguments validation = Assert.Single(cut.Instance.Validations);
        Assert.Equal(1d, validation.Value);
        Assert.Equal(["quantity"], validation.Values.Keys);
        Assert.Equal(1d, validation.Values["quantity"]);
    }

    [Fact(DisplayName = "is validated with clamped value when validationMode=onBlur")]
    [Trait("Requirement", "NUM-FRM-09")]
    [Trait("Requirement", "NUM-EVT-09")]
    public async Task Is_validated_with_clamped_value_when_validationMode_onBlur()
    {
        IRenderedComponent<NumberFieldAutofillScenarios> cut = Render(Scenario.OnBlurClamped);

        await FocusAsync(cut);
        await ChangeAsync(cut, "1000");
        await BlurAsync(cut, "1000");

        FieldValidationArguments validation = Assert.Single(cut.Instance.Validations);
        Assert.Equal(10d, validation.Value);
        Assert.Equal(["quantity"], validation.Values.Keys);
        Assert.Equal(10d, validation.Values["quantity"]);
        Assert.Equal("10", Text);
    }

    [Fact(DisplayName = "revalidates an external change after a blur that normalizes back to the current value")]
    [Trait("Requirement", "NUM-FRM-08")]
    public async Task Revalidates_an_external_change_after_a_blur_that_normalizes_back_to_the_current_value()
    {
        IRenderedComponent<NumberFieldAutofillScenarios> cut = Render(Scenario.ExternalAfterBlur);

        // Blur after typing a value that clamps back to the current value (5): the blur commits an error, and the stored
        // value is unchanged.
        await FocusAsync(cut);
        await ChangeAsync(cut, "9");
        await BlurAsync(cut, "9");
        Assert.Equal("5", Text);
        Assert.Equal("true", Input(cut).GetAttribute("aria-invalid"));

        // The next external change revalidates and clears the error rather than being swallowed.
        await cut.Find("button").ClickAsync(new());
        await SettleAsync(cut);
        Assert.False(Input(cut).HasAttribute("aria-invalid"));
    }

    [Fact(DisplayName = "clears the value when autofill empties the hidden input")]
    [Trait("Requirement", "NUM-FRM-05")]
    public async Task Clears_the_value_when_autofill_empties_the_hidden_input()
    {
        IRenderedComponent<NumberFieldAutofillScenarios> cut = Render(Scenario.Plain, defaultValue: 5);

        await AutofillAsync(cut, null);

        Assert.Equal(string.Empty, Text);
        Assert.Null(cut.Instance.Changes[^1]);
    }

    [Fact(DisplayName = "validates the autofilled value even when the change is canceled")]
    [Trait("Requirement", "NUM-FRM-05")]
    public async Task Validates_the_autofilled_value_even_when_the_change_is_canceled()
    {
        IRenderedComponent<NumberFieldAutofillScenarios> cut = Render(Scenario.CanceledInField);

        await AutofillAsync(cut, 7);

        Assert.Equal(string.Empty, Text);
        Assert.NotEmpty(cut.Instance.Validations);
        Assert.Equal(7d, cut.Instance.Validations[^1].Value);
    }

    private IRenderedComponent<NumberFieldAutofillScenarios> Render(Scenario scenario, string? lockState = null, double? defaultValue = null) =>
        host.Context.Render<NumberFieldAutofillScenarios>(p => p
            .Add(x => x.Scenario, scenario)
            .Add(x => x.ReadOnly, lockState == "readOnly")
            .Add(x => x.Disabled, lockState == "disabled")
            .Add(x => x.DefaultValue, defaultValue));

    private NumberFieldTwin Twin() =>
        ((DotNetObjectReference<NumberFieldTwin>)numberFieldModule.Invocations["attach"][^1].Arguments[2]!).Value;

    // A report as the `number-field` module sends it, then the `form` module's answers to the refreshes it caused.
    private async Task ReportAsync(IRenderedComponent<NumberFieldAutofillScenarios> cut, Action<NumberFieldTwin> report)
    {
        NumberFieldTwin twin = Twin();
        await cut.InvokeAsync(() => report(twin));
        await SettleAsync(cut);
    }

    private async Task SettleAsync(IRenderedComponent<NumberFieldAutofillScenarios> cut)
    {
        // A commit that awaited a refresh may ask for the next one when it resumes.
        for (int i = 0; i < 3; i++)
        {
            host.Module.Settle(cut);
            await cut.InvokeAsync(() => { });
        }
    }

    private Task AutofillAsync(IRenderedComponent<NumberFieldAutofillScenarios> cut, double? value) =>
        ReportAsync(cut, t => t.OnAutofill(JsonSerializer.Serialize(new { value, @event = new { kind = "Event", type = "change" } })));

    private Task FocusAsync(IRenderedComponent<NumberFieldAutofillScenarios> cut) =>
        ReportAsync(cut, t => t.OnInputFocus("""{"event":{"kind":"FocusEvent","type":"focus"}}"""));

    private Task ChangeAsync(IRenderedComponent<NumberFieldAutofillScenarios> cut, string text) =>
        ReportAsync(cut, t => t.OnInputChange(JsonSerializer.Serialize(new { text, revision = ++revision, @event = new { kind = "InputEvent", type = "input" } })));

    private Task BlurAsync(IRenderedComponent<NumberFieldAutofillScenarios> cut, string text) =>
        ReportAsync(cut, t => t.OnInputBlur(JsonSerializer.Serialize(new { text, revision = ++revision, @event = new { kind = "FocusEvent", type = "blur" } })));

    private static IElement Input(IRenderedComponent<NumberFieldAutofillScenarios> cut) => cut.Find("input[type=text]");
}
