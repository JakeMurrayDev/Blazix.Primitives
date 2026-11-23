using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// CMB-FRM-01 at L2 (ComboboxRoot.test.tsx, describe "Form"): a Form's submission carries the combobox's stringified value
// under the field's name, and an Input rendered as the field-aware Input submits, outside the popup with the field's name and
// inside it without one. The submission is the `form` module's report (SubmitAsync). The two external-form titles (lines
// 11673 and 11722) are skipIf(isJSDOM), so L4; the title of line 11914 needs the browser's constraint validation of the
// required hidden input, which the `form` module reads (CMB-FRM-02 cites it at L4), so it has no test here.
/// <summary>ComboboxRoot in a Form at L2.</summary>
public sealed class ComboboxRootFormTests : IDisposable
{
    private static readonly Country[] s_countries = [new("US", "United States"), new("CA", "Canada")];

    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "submits stringified value to onFormSubmit when itemToStringValue is provided")]
    [Trait("Requirement", "CMB-FRM-01")]
    public async Task Submits_stringified_value_to_onFormSubmit_when_itemToStringValue_is_provided()
    {
        IRenderedComponent<ComboboxFieldHost> cut = RenderForm("country", Typed<Country>(new()
        {
            ["Items"] = (IReadOnlyList<Country>)s_countries,
            ["ItemToStringLabel"] = (Func<Country, string>)(item => item.Label),
            ["ItemToStringValue"] = (Func<Country, string>)(item => item.Code),
            ["DefaultValue"] = s_countries[0],
        }));

        await test.SubmitAsync(cut);

        FormValues values = Assert.Single(cut.Instance.FormSubmits);
        Assert.Equal("US", values["country"]);
    }

    [Fact(DisplayName = "submits when input renders a field-aware input")]
    [Trait("Requirement", "CMB-FRM-01")]
    public async Task Submits_when_input_renders_a_field_aware_input()
    {
        void Content(RenderTreeBuilder builder)
        {
            builder.OpenComponent<ComboboxHarness>(0);
            builder.AddComponentParameter(1, nameof(ComboboxHarness.RootParameters), new Dictionary<string, object?> { ["Items"] = (IReadOnlyList<string>)["France", "Germany"], ["Required"] = true });
            builder.AddComponentParameter(2, nameof(ComboboxHarness.WithInput), false);
            builder.AddComponentParameter(3, nameof(ComboboxHarness.Extra), FieldAwareInput);
            builder.CloseComponent();
        }

        IRenderedComponent<ComboboxFieldHost> cut = RenderForm("country", Content);
        Assert.Equal("country", cut.Find("[data-testid=input]").GetAttribute("name"));

        await test.PressAsync(cut, "[data-testid=input]", "input-press");
        await test.ClickOptionNamedAsync(cut, "France");
        await test.SubmitAsync(cut);

        FormValues values = Assert.Single(cut.Instance.FormSubmits);
        Assert.Equal("France", values["country"]);
    }

    [Fact(DisplayName = "submits when input inside popup renders a field-aware input")]
    [Trait("Requirement", "CMB-FRM-01")]
    public async Task Submits_when_input_inside_popup_renders_a_field_aware_input()
    {
        IRenderedComponent<ComboboxFieldHost> cut = RenderForm("country", InsidePopup(new() { ["Items"] = (IReadOnlyList<string>)["France", "Germany"], ["Required"] = true }));

        await test.PressAsync(cut, "[data-testid=trigger]", "trigger-press");
        Assert.False(cut.Find("[data-testid=input]").HasAttribute("name"));
        await test.ClickOptionNamedAsync(cut, "France");

        Assert.NotNull(cut.Find("input[aria-hidden=true][name=country]"));
        Assert.Contains("\"value\":\"France\"", LastFormControlOptions(), StringComparison.Ordinal);

        await test.SubmitAsync(cut);

        FormValues values = Assert.Single(cut.Instance.FormSubmits);
        Assert.Equal("France", values["country"]);
    }

    // AriaCombobox.tsx:1691-1702 (FFI §6.3 Table A): the hidden input is the field's validation input. The port reads its native
    // state from the `form` module's reports (DP6, as the checkable controls and Select do), the first one asked at attach;
    // a named hidden input renders no id, so the submit validates from the last report.
    [Fact(DisplayName = "port: a required combobox's hidden input reports its native state to the field, which blocks the submit and shows the error")]
    [Trait("Requirement", "CMB-FRM-02")]
    [Trait("Requirement", "CMB-FOC-05")]
    public async Task A_required_hidden_input_blocks_the_submit_from_its_reported_state()
    {
        void Content(RenderTreeBuilder builder)
        {
            builder.OpenComponent<ComboboxHarness>(0);
            builder.AddComponentParameter(1, nameof(ComboboxHarness.RootParameters), new Dictionary<string, object?> { ["Items"] = (IReadOnlyList<string>)["France", "Germany"], ["Required"] = true });
            builder.CloseComponent();
        }

        IRenderedComponent<ComboboxFieldHost> cut = test.Context.Render<ComboboxFieldHost>(p => p
            .Add(x => x.InForm, true)
            .Add(x => x.CaptureFormSubmit, true)
            .Add(x => x.FieldName, "country")
            .Add(x => x.ErrorMatch, (MatchValue)ValidityFlag.ValueMissing)
            .Add(x => x.ErrorText, "required")
            .Add(x => x.Content, (RenderFragment)Content));
        FormControlOptions control = System.Text.Json.JsonSerializer.Deserialize(LastFormControlOptions(), BlazixJsonContext.Default.FormOptions)!.Control!;
        Assert.NotNull(control.FormId);
        Assert.True(control.Revision > 1);

        FormControlReport report = new() { Type = "sync", Validity = new NativeValidity { Valid = false, ValueMissing = true, WillValidate = true, Eligible = true }, Revision = control.Revision };
        string json = System.Text.Json.JsonSerializer.Serialize(report, BlazixJsonContext.Default.FormControlReport);
        await cut.InvokeAsync(() => HiddenControlTwin().OnControlReport(json));
        FormSubmitAnswer answer = await test.SubmitAsync(cut);

        Assert.Equal(FormSubmitAnswer.BlockedVerdict, answer.Verdict);
        Assert.Empty(cut.Instance.FormSubmits);
        Assert.Equal(cut.Find("[data-testid=input]").Id, Assert.Single(answer.Focus!));
        Assert.Equal("required", cut.Find("[data-testid=error]").TextContent);
    }

    private static RenderFragment FieldAwareInput => builder =>
    {
        builder.OpenComponent<ComboboxInput>(0);
        builder.AddComponentParameter(1, nameof(ComboboxInput.RenderFunction), (RenderFragment<RenderProps<ComboboxInputState>>)(props => inner =>
        {
            inner.OpenComponent<Input>(0);
            inner.AddMultipleAttributes(1, props.Attributes);
            inner.AddAttribute(2, "data-testid", "input");
            inner.CloseComponent();
        }));
        builder.CloseComponent();
    };

    private static RenderFragment InsidePopup(Dictionary<string, object?> root) => builder =>
    {
        builder.OpenComponent<ComboboxPopupInputHost<string>>(0);
        builder.AddComponentParameter(1, nameof(ComboboxPopupInputHost<string>.RootParameters), root);
        builder.AddComponentParameter(2, nameof(ComboboxPopupInputHost<string>.TriggerContent), (RenderFragment)(value =>
        {
            value.OpenComponent<ComboboxValue<string>>(0);
            value.CloseComponent();
        }));
        builder.AddComponentParameter(3, nameof(ComboboxPopupInputHost<string>.InputContent), FieldAwareInput);
        builder.CloseComponent();
    };

    private static RenderFragment Typed<TValue>(Dictionary<string, object?> root) => builder =>
    {
        builder.OpenComponent<ComboboxTypedHarness<TValue>>(0);
        builder.AddComponentParameter(1, nameof(ComboboxTypedHarness<TValue>.RootParameters), root);
        builder.CloseComponent();
    };

    private FormTwin HiddenControlTwin() =>
        ((DotNetObjectReference<FormTwin>)test.Form.Invocations["attach"].Last(attach => attach.Arguments[1] is string json && json.Contains("\"reportAutofill\":true", StringComparison.Ordinal)).Arguments[2]!).Value;

    private string LastFormControlOptions()
    {
        string? options = null;
        foreach (JSRuntimeInvocation attach in test.Form.Invocations["attach"])
        {
            if (attach.Arguments[1] is string json && json.Contains("\"reportAutofill\":true", StringComparison.Ordinal))
            {
                options = json;
            }
        }

        foreach (JSRuntimeInvocation update in test.FormHandle.Invocations["update"])
        {
            if (update.Arguments[0] is string json && json.Contains("\"reportAutofill\":true", StringComparison.Ordinal))
            {
                options = json;
            }
        }

        return options ?? string.Empty;
    }

    private IRenderedComponent<ComboboxFieldHost> RenderForm(string name, RenderFragment content) =>
        test.Context.Render<ComboboxFieldHost>(p => p
            .Add(x => x.InForm, true)
            .Add(x => x.CaptureFormSubmit, true)
            .Add(x => x.FieldName, name)
            .Add(x => x.Content, content));

    private sealed record Country(string Code, string Label);
}
