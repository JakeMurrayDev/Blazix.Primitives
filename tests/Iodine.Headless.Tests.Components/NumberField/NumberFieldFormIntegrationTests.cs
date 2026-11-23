using System.Globalization;
using System.Text.Json;
using AngleSharp.Dom;
using Iodine.Headless.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Scenario = Iodine.Headless.Tests.Components.NumberFieldFormIntegrationScenarios.IntegrationScenario;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Iodine.Headless.Tests.Components;

// 6b's batch C at L2 (issue 277): NumberFieldRoot.test.tsx's Field and Form titles (the field state attributes, the
// validation modes, external errors, autofill) and its root-level typing and stepping titles whose text also names an L1
// helper title (parse.test.ts, validate.test.ts): these are the component versions, which drive the root. Base UI's
// fireEvent.change, focus and blur on the visible input are the `number-field` module's change, focus and blur reports, its
// fireEvent.change on the hidden input the module's autofill report, and its stepper click the `button` module's click report
// (ADR 06 §1). A field's validation reads the hidden input's native state from a fresh `form` report first, so the `form`
// module is the Field tests' stand-in (FieldTestContext), which answers the refreshes each report caused; a field error
// leaves after its exit transition, which the stand-in ends. Two port titles cover NUM-FOC-06 and NUM-EDGE-08, which Base UI
// does not test.
/// <summary>A number field inside a field or a form, and the root's parsing and stepping of typed and stepped values.</summary>
public sealed class NumberFieldFormIntegrationTests : IDisposable
{
    private const string ButtonModulePath = "./_content/Iodine.Headless/js/button.js";
    private static readonly CultureInfo s_enUs = CultureInfo.GetCultureInfo("en-US");

    private readonly FieldTestContext host = new();
    private readonly BunitJSModuleInterop numberFieldModule;
    private readonly BunitJSModuleInterop numberFieldHandle;
    private readonly BunitJSModuleInterop buttonModule;
    private int revision;

    /// <summary>Initializes a new instance of the <see cref="NumberFieldFormIntegrationTests"/> class.</summary>
    public NumberFieldFormIntegrationTests()
    {
        host.Context.JSInterop.SetupModule(NumberFieldTestContext.InteractionsModulePath)
            .Setup<string>("readPlatformFlags")
            .SetResult("""{"voiceOver":false,"mac":false,"ios":false}""");
        numberFieldModule = host.Context.JSInterop.SetupModule(NumberFieldTestContext.NumberFieldModulePath);
        numberFieldHandle = numberFieldModule.SetupModule("attach", _ => true);
        numberFieldHandle.SetupVoid("update", _ => true).SetVoidResult();
        numberFieldHandle.SetupVoid("dispose", _ => true).SetVoidResult();
        buttonModule = host.Context.JSInterop.SetupModule(ButtonModulePath);
        BunitJSModuleInterop buttonHandle = buttonModule.SetupModule("attach", _ => true);
        buttonHandle.SetupVoid("update", _ => true).SetVoidResult();
        buttonHandle.SetupVoid("dispose", _ => true).SetVoidResult();
    }

    // The text the visible input shows: the last text sent to the `number-field` module for the input.
    private string Text
    {
        get
        {
            string[] options =
            [
                .. numberFieldModule.Invocations["attach"].Select(i => (string)i.Arguments[1]!),
                .. numberFieldHandle.Invocations["update"].Select(i => (string)i.Arguments[0]!),
            ];
            return JsonDocument.Parse(options.Last(o => !o.Contains("\"part\":", StringComparison.Ordinal))).RootElement.GetProperty("value").GetString()!;
        }
    }

    /// <inheritdoc />
    public void Dispose() => host.Dispose();

    [Fact(DisplayName = "should be called when the value changes")]
    [Trait("Requirement", "NUM-API-03")]
    [Trait("Requirement", "NUM-EVT-01")]
    public async Task Should_be_called_when_the_value_changes()
    {
        IRenderedComponent<NumberFieldFormIntegrationScenarios> cut = Render(Scenario.Controlled, initialValue: 1);

        await ChangeAsync(cut, "2");

        Assert.Equal([2d], cut.Instance.Changes);
    }

    // The component version of validate.test.ts's title (NumberStepValidationTests runs that one at L1).
    [Fact(DisplayName = "preserves large fractional values when stepping cleanup would be too coarse")]
    [Trait("Requirement", "NUM-STA-11")]
    public async Task Preserves_large_fractional_values_when_stepping_cleanup_would_be_too_coarse()
    {
        IRenderedComponent<NumberFieldFormIntegrationScenarios> cut = Render(Scenario.Controlled, initialValue: 100000000000000.1, step: 0.1);

        await ClickIncrementAsync(cut);

        Assert.Equal(100000000000000.1 + 0.1, cut.Instance.Changes[^1]);
    }

    [Fact(DisplayName = "clears external errors on change")]
    [Trait("Requirement", "NUM-FRM-08")]
    [Trait("Requirement", "NUM-FRM-12")]
    [Trait("Requirement", "NUM-ARIA-02")]
    public async Task Clears_external_errors_on_change()
    {
        IRenderedComponent<NumberFieldFormIntegrationScenarios> cut = Render(Scenario.ExternalErrors);

        Assert.Equal("true", Input(cut).GetAttribute("aria-invalid"));
        Assert.Equal("test", cut.Find("[data-testid=error]").TextContent);

        await ChangeAsync(cut, "5");
        host.CompleteExits(cut);

        Assert.False(Input(cut).HasAttribute("aria-invalid"));
        Assert.Empty(cut.FindAll("[data-testid=error]"));
    }

    [Fact(DisplayName = "should handle browser autofill")]
    [Trait("Requirement", "NUM-FRM-05")]
    public async Task Should_handle_browser_autofill()
    {
        IRenderedComponent<NumberFieldFormIntegrationScenarios> cut = Render(Scenario.Autofill);
        Assert.Single(cut.FindAll("input[type=number][name=quantity]"));

        await AutofillAsync(cut, 42);

        Assert.Equal([42d], cut.Instance.Changes);
        Assert.Equal("42", Text);
    }

    [Fact(DisplayName = "[data-dirty]")]
    [Trait("Requirement", "NUM-STA-14")]
    [Trait("Requirement", "NUM-DOM-04")]
    public async Task Data_dirty()
    {
        IRenderedComponent<NumberFieldFormIntegrationScenarios> cut = Render(Scenario.Plain);

        Assert.False(Input(cut).HasAttribute("data-dirty"));

        await ChangeAsync(cut, "1");

        Assert.Equal(string.Empty, Input(cut).GetAttribute("data-dirty"));
    }

    [Fact(DisplayName = "adds [data-filled] attribute when filled")]
    [Trait("Requirement", "NUM-STA-13")]
    [Trait("Requirement", "NUM-DOM-04")]
    public async Task Adds_data_filled_attribute_when_filled()
    {
        IRenderedComponent<NumberFieldFormIntegrationScenarios> cut = Render(Scenario.Plain);

        Assert.False(Input(cut).HasAttribute("data-filled"));

        await ChangeAsync(cut, "1");
        Assert.Equal(string.Empty, Input(cut).GetAttribute("data-filled"));

        await ChangeAsync(cut, string.Empty);
        Assert.False(Input(cut).HasAttribute("data-filled"));
    }

    // Base UI's input.focus() is the module's focus report; the unmount is a re-render without the number field.
    [Fact(DisplayName = "is removed when the focused input unmounts")]
    [Trait("Requirement", "NUM-FOC-05")]
    public async Task Is_removed_when_the_focused_input_unmounts()
    {
        IRenderedComponent<NumberFieldFormIntegrationScenarios> cut = Render(Scenario.Unmount);

        await FocusAsync(cut);
        Assert.Equal(string.Empty, cut.Find("[data-testid=root]").GetAttribute("data-focused"));

        cut.Render(p => p.Add(x => x.FirstMounted, false));

        Assert.False(cut.Find("[data-testid=root]").HasAttribute("data-focused"));
    }

    [Fact(DisplayName = "prop: validate")]
    [Trait("Requirement", "NUM-FRM-09")]
    [Trait("Requirement", "NUM-ARIA-02")]
    public async Task Prop_validate()
    {
        IRenderedComponent<NumberFieldFormIntegrationScenarios> cut = Render(Scenario.PropValidate);

        Assert.False(Input(cut).HasAttribute("aria-invalid"));

        await FocusAsync(cut);
        await BlurAsync(cut, string.Empty);

        Assert.Equal("true", Input(cut).GetAttribute("aria-invalid"));
    }

    [Fact(DisplayName = "onChange")]
    [Trait("Requirement", "NUM-FRM-08")]
    [Trait("Requirement", "NUM-ARIA-02")]
    public async Task OnChange()
    {
        IRenderedComponent<NumberFieldFormIntegrationScenarios> cut = Render(Scenario.OnChange);

        Assert.False(Input(cut).HasAttribute("aria-invalid"));

        await ChangeAsync(cut, "1");

        Assert.Equal("true", Input(cut).GetAttribute("aria-invalid"));
    }

    [Fact(DisplayName = "revalidates when the controlled value changes externally")]
    [Trait("Requirement", "NUM-FRM-08")]
    public async Task Revalidates_when_the_controlled_value_changes_externally()
    {
        IRenderedComponent<NumberFieldFormIntegrationScenarios> cut = Render(Scenario.ExternalControlled);

        Assert.False(Input(cut).HasAttribute("aria-invalid"));
        int initialCallCount = cut.Instance.Validations.Count;

        await cut.Find("button").ClickAsync(new());
        await SettleAsync(cut);

        Assert.Equal(initialCallCount + 1, cut.Instance.Validations.Count);
        Assert.Equal(5d, cut.Instance.Validations[^1].Value);
        Assert.Equal("true", Input(cut).GetAttribute("aria-invalid"));
    }

    [Fact(DisplayName = "onBlur")]
    [Trait("Requirement", "NUM-FRM-09")]
    public async Task OnBlur()
    {
        IRenderedComponent<NumberFieldFormIntegrationScenarios> cut = Render(Scenario.OnBlur);
        Assert.False(Input(cut).HasAttribute("aria-invalid"));

        await ChangeAsync(cut, "1");
        Assert.False(Input(cut).HasAttribute("aria-invalid"));
        await BlurAsync(cut, "1");
        Assert.Equal("true", Input(cut).GetAttribute("aria-invalid"));

        // revalidation
        await ChangeAsync(cut, "2");
        host.CompleteExits(cut);
        Assert.False(Input(cut).HasAttribute("aria-invalid"));
        Assert.Empty(cut.FindAll("[data-testid=error]"));
    }

    // The component version of parse.test.ts's title (NumberParserTests runs that one at L1).
    [Fact(DisplayName = "parses fullwidth digits and punctuation")]
    [Trait("Requirement", "NUM-I18N-04")]
    public async Task Parses_fullwidth_digits_and_punctuation()
    {
        IRenderedComponent<NumberFieldFormIntegrationScenarios> cut = Render(Scenario.Controlled);

        await ChangeAsync(cut, "１，２３４．５６");

        Assert.Equal([1234.56], cut.Instance.Changes);
    }

    // The component version of parse.test.ts's title (NumberParserTests runs that one at L1).
    [Fact(DisplayName = "collapses extra dots from mixed-locale inputs")]
    [Trait("Requirement", "NUM-I18N-09")]
    public async Task Collapses_extra_dots_from_mixed_locale_inputs()
    {
        IRenderedComponent<NumberFieldFormIntegrationScenarios> cut = Render(Scenario.Controlled);

        await ChangeAsync(cut, "1.234.567.89");

        Assert.Equal([1234567.89], cut.Instance.Changes);
    }

    // NUM-FOC-06 (Base UI has no title): both steppers render tabindex="-1", and their button mechanism is focusable when
    // disabled, so a disabled NativeButton=false stepper keeps aria-disabled="true" and no `disabled` attribute (it stays
    // programmatically focusable), while a disabled native stepper carries `disabled`.
    [Fact(DisplayName = "port: the steppers render tabindex=\"-1\" and a disabled NativeButton=false stepper stays focusable")]
    [Trait("Requirement", "NUM-FOC-06")]
    public void The_steppers_render_tabindex_minus_one_and_a_disabled_non_native_stepper_stays_focusable()
    {
        IRenderedComponent<NumberFieldRoot> cut = host.Context.Render<NumberFieldRoot>(p => p
            .Add(x => x.Culture, s_enUs)
            .Add(x => x.Disabled, true)
            .Add(x => x.ChildContent, (RenderFragment)(b =>
            {
                b.OpenComponent<NumberFieldDecrement>(0);
                b.AddAttribute(1, "data-testid", "decrement");
                b.CloseComponent();
                b.OpenComponent<NumberFieldInput>(2);
                b.CloseComponent();
                b.OpenComponent<NumberFieldIncrement>(3);
                b.AddComponentParameter(4, nameof(NumberFieldIncrement.NativeButton), false);
                b.AddAttribute(5, "data-testid", "increment");
                b.CloseComponent();
            })));

        IElement decrement = cut.Find("[data-testid=decrement]");
        IElement increment = cut.Find("[data-testid=increment]");
        Assert.Equal("-1", decrement.GetAttribute("tabindex"));
        Assert.Equal("-1", increment.GetAttribute("tabindex"));

        Assert.True(decrement.HasAttribute("disabled"));
        Assert.False(increment.HasAttribute("disabled"));
        Assert.Equal("true", increment.GetAttribute("aria-disabled"));
        Assert.All(
            buttonModule.Invocations["attach"],
            attach =>
            {
                JsonElement options = JsonDocument.Parse((string)attach.Arguments[1]!).RootElement;
                Assert.True(options.GetProperty("focusableWhenDisabled").GetBoolean());
                Assert.True(options.GetProperty("disabled").GetBoolean());
            });
        Assert.Equal(2, buttonModule.Invocations["attach"].Count);
    }

    // NUM-EDGE-08 (Base UI has no title): with SnapOnStep, an Alt step by a zero SmallStep skips the snapping, whose division
    // by the step would make the value NaN, and leaves the value; a negative Step and LargeStep step the other way.
    [Fact(DisplayName = "port: snapping is skipped for a zero step amount and a negative step inverts the direction")]
    [Trait("Requirement", "NUM-EDGE-08")]
    public async Task Snapping_is_skipped_for_a_zero_step_amount_and_a_negative_step_inverts_the_direction()
    {
        List<double?> changes = [];
        IRenderedComponent<NumberFieldRoot> cut = host.Context.Render<NumberFieldRoot>(p => p
            .Add(x => x.Culture, s_enUs)
            .Add(x => x.DefaultValue, 0.15)
            .Add(x => x.SnapOnStep, true)
            .Add(x => x.SmallStep, 0)
            .Add(x => x.OnValueChange, (ValueChangeEventArgs<double?> e) => changes.Add(e.Value))
            .Add(x => x.ChildContent, (RenderFragment)(b =>
            {
                b.OpenComponent<NumberFieldInput>(0);
                b.CloseComponent();
            })));

        await KeyAsync(cut, "ArrowUp", altKey: true);
        Assert.Equal("0.15", Text);
        Assert.DoesNotContain(changes, v => v is double d && double.IsNaN(d));

        cut.Render(p => p.Add(x => x.SnapOnStep, false).Add(x => x.Step, (StepValue?)(-1)).Add(x => x.LargeStep, -10));
        await KeyAsync(cut, "ArrowUp");
        Assert.Equal(-0.85, changes[^1]);
        await KeyAsync(cut, "ArrowUp", shiftKey: true);
        Assert.Equal(-10.85, changes[^1]);
        Assert.Equal("-10.85", Text);
    }

    private IRenderedComponent<NumberFieldFormIntegrationScenarios> Render(Scenario scenario, double? initialValue = null, double? step = null) =>
        host.Context.Render<NumberFieldFormIntegrationScenarios>(p => p
            .Add(x => x.Scenario, scenario)
            .Add(x => x.InitialValue, initialValue)
            .Add(x => x.Step, step is double s ? (StepValue?)s : null));

    private NumberFieldTwin Twin() =>
        numberFieldModule.Invocations["attach"].Select(i => i.Arguments[2]).OfType<DotNetObjectReference<NumberFieldTwin>>().Last().Value;

    // A report as the `number-field` module sends it, then the `form` module's answers to the refreshes it caused.
    private async Task ReportAsync<TComponent>(IRenderedComponent<TComponent> cut, Action<NumberFieldTwin> report)
        where TComponent : IComponent
    {
        NumberFieldTwin twin = Twin();
        await cut.InvokeAsync(() => report(twin));
        await SettleAsync(cut);
    }

    private async Task SettleAsync<TComponent>(IRenderedComponent<TComponent> cut)
        where TComponent : IComponent
    {
        // A commit that awaited a refresh may ask for the next one when it resumes.
        for (int i = 0; i < 3; i++)
        {
            host.Module.Settle(cut);
            await cut.InvokeAsync(() => { });
        }
    }

    private Task AutofillAsync<TComponent>(IRenderedComponent<TComponent> cut, double? value)
        where TComponent : IComponent =>
        ReportAsync(cut, t => t.OnAutofill(JsonSerializer.Serialize(new { value, @event = new { kind = "Event", type = "change" } })));

    private Task FocusAsync<TComponent>(IRenderedComponent<TComponent> cut)
        where TComponent : IComponent =>
        ReportAsync(cut, t => t.OnInputFocus("""{"event":{"kind":"FocusEvent","type":"focus"}}"""));

    private Task ChangeAsync<TComponent>(IRenderedComponent<TComponent> cut, string text)
        where TComponent : IComponent =>
        ReportAsync(cut, t => t.OnInputChange(JsonSerializer.Serialize(new { text, revision = ++revision, @event = new { kind = "InputEvent", type = "input" } })));

    private Task BlurAsync<TComponent>(IRenderedComponent<TComponent> cut, string text)
        where TComponent : IComponent =>
        ReportAsync(cut, t => t.OnInputBlur(JsonSerializer.Serialize(new { text, revision = ++revision, @event = new { kind = "FocusEvent", type = "blur" } })));

    private Task KeyAsync<TComponent>(IRenderedComponent<TComponent> cut, string key, bool altKey = false, bool shiftKey = false)
        where TComponent : IComponent
    {
        string text = Text;
        return ReportAsync(cut, t => t.OnKeyStep(JsonSerializer.Serialize(new { key, text, revision = ++revision, @event = new { kind = "KeyboardEvent", type = "keydown", key, altKey, shiftKey } })));
    }

    // A single-action click on the increment, as the button module reports a keyboard click or a tap.
    private Task ClickIncrementAsync(IRenderedComponent<NumberFieldFormIntegrationScenarios> cut)
    {
        ElementReference? element = cut.FindComponent<NumberFieldIncrement>().Instance.Element;
        JSRuntimeInvocation attach = buttonModule.Invocations["attach"].Last(i => Equals(i.Arguments[0], element));
        ButtonTwin twin = ((DotNetObjectReference<ButtonTwin>)attach.Arguments[2]!).Value;
        string json = $$"""{"event":{{NumberFieldTestContext.EventJson("click", "", 0)}}}""";
        return cut.InvokeAsync(() => twin.OnHoldClick(json));
    }

    private static IElement Input<TComponent>(IRenderedComponent<TComponent> cut)
        where TComponent : IComponent => cut.Find("input[type=text]");
}
