using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// CMB §8 at L2 (ComboboxRoot.test.tsx, describe "Field"): with the input inside the popup the trigger is the labelled and
// validated control and the input gets no field state; the label's press focuses the trigger without opening (the `form`
// module's non-native label role focuses the control id C# sends it); validate receives the raw value; and Combobox.Label
// beside an input outside the popup labels nothing and logs the development warning of CMB-STR-10.
/// <summary>ComboboxRoot inside a Field at L2.</summary>
public sealed class ComboboxRootFieldTests : IDisposable
{
    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "Field.Label links to Combobox.Trigger when input is inside popup and trigger has an explicit id")]
    [Trait("Requirement", "CMB-API-45")]
    [Trait("Requirement", "CMB-ARIA-16")]
    [Trait("Requirement", "CMB-FRM-06")]
    public void Field_Label_links_to_the_trigger_with_an_explicit_id()
    {
        IRenderedComponent<ComboboxFieldHost> cut = test.Context.Render<ComboboxFieldHost>(p => p
            .Add(x => x.WithFieldLabel, true)
            .Add(x => x.Content, InsidePopup(Parts(label: false, triggerId: "x-id"))));

        IElement label = cut.Find("[data-testid=label]");
        IElement trigger = cut.Find("[data-testid=trigger]");
        Assert.Equal(label.Id, trigger.GetAttribute("aria-labelledby"));
        Assert.Equal("x-id", trigger.Id);
    }

    [Fact(DisplayName = "does not apply validation ARIA attributes to input inside popup")]
    [Trait("Requirement", "CMB-STA-05")]
    [Trait("Requirement", "CMB-ARIA-06")]
    public async Task Does_not_apply_validation_ARIA_attributes_to_input_inside_popup()
    {
        IRenderedComponent<ComboboxFieldHost> cut = test.Context.Render<ComboboxFieldHost>(p => p
            .Add(x => x.FieldInvalid, true)
            .Add(x => x.WithDescription, true)
            .Add(x => x.ErrorMatch, true)
            .Add(x => x.Content, InsidePopup(Parts(label: false))));
        IElement trigger = cut.Find("[data-testid=trigger]");
        string describedBy = trigger.GetAttribute("aria-describedby") ?? string.Empty;

        Assert.Equal("true", trigger.GetAttribute("aria-invalid"));
        Assert.Contains(cut.Find("[data-testid=description]").Id!, describedBy, StringComparison.Ordinal);
        Assert.Contains(cut.Find("[data-testid=error]").Id!, describedBy, StringComparison.Ordinal);

        await test.PressAsync(cut, "[data-testid=trigger]", "trigger-press");
        IElement input = cut.Find("[data-testid=input]");

        Assert.False(input.HasAttribute("aria-invalid"));
        Assert.False(input.HasAttribute("aria-describedby"));
        Assert.False(input.HasAttribute("data-valid"));
        Assert.False(input.HasAttribute("data-invalid"));
        Assert.False(input.HasAttribute("data-touched"));
        Assert.False(input.HasAttribute("data-dirty"));
        Assert.False(input.HasAttribute("data-filled"));
        Assert.False(input.HasAttribute("data-focused"));
    }

    // FFI-PTR-01: the label's click is the `form` module's, which focuses the control id C# sends; the trigger opens on a
    // press, not on focus.
    [Fact(DisplayName = "Combobox.Label focuses trigger without opening when input is inside popup")]
    [Trait("Requirement", "CMB-DOM-17")]
    public async Task Combobox_Label_focuses_the_trigger_without_opening()
    {
        IRenderedComponent<ComboboxHarness> cut = test.Context.Render<ComboboxHarness>(p => p
            .Add(x => x.StaticItems, ["a", "b"])
            .Add(x => x.InputInsidePopup, true)
            .Add(x => x.Extra, Parts(label: true)));

        await cut.InvokeAsync(() => cut.Find("[data-testid=label]").Click());

        string triggerId = cut.Find("[data-testid=trigger]").Id!;
        Assert.Contains("\"controlId\":\"" + triggerId + "\"", LabelOptions(), StringComparison.Ordinal);
        Assert.False(cut.Instance.Root!.Store.IsOpen);
        Assert.Empty(cut.FindAll("[role=dialog]"));
    }

    [Fact(DisplayName = "passes raw value to validate when itemToStringValue is provided")]
    [Trait("Requirement", "CMB-API-33")]
    public async Task Passes_raw_value_to_validate_when_itemToStringValue_is_provided()
    {
        Country[] items = [new("US", "United States"), new("CA", "Canada")];
        List<object?> validated = [];
        IRenderedComponent<ComboboxFieldHost> cut = test.Context.Render<ComboboxFieldHost>(p => p
            .Add(x => x.ValidationMode, ValidationMode.OnBlur)
            .Add(x => x.Validate, args =>
            {
                validated.Add(args.Value);
                return ValueTask.FromResult<FieldValidationResult?>("error");
            })
            .Add(x => x.Content, builder =>
            {
                builder.OpenComponent<ComboboxTypedHarness<Country>>(0);
                builder.AddComponentParameter(1, nameof(ComboboxTypedHarness<Country>.RootParameters), new Dictionary<string, object?>
                {
                    ["Items"] = (IReadOnlyList<Country>)items,
                    ["DefaultValue"] = items[0],
                    ["ItemToStringLabel"] = (Func<Country, string>)(item => item.Label),
                    ["ItemToStringValue"] = (Func<Country, string>)(item => item.Code),
                });
                builder.AddComponentParameter(2, nameof(ComboboxTypedHarness<Country>.WithList), false);
                builder.CloseComponent();
            }));

        await cut.InvokeAsync(() => cut.Find("[data-testid=input]").Focus());
        await cut.InvokeAsync(() => cut.Find("[data-testid=input]").Blur());

        Assert.Same(items[0], Assert.Single(validated));
        Assert.Equal("true", cut.Find("[data-testid=input]").GetAttribute("aria-invalid"));
    }

    // AriaCombobox.tsx:838-850 for "validates when the popup is blurred": the store's half, a close by focus-out from inside the
    // popup, which the focus manager requests (its focus-out close is L4).
    [Fact(DisplayName = "port: a focus-out close from inside the popup touches the field, ends its focus and validates in onBlur mode")]
    [Trait("Requirement", "CMB-FOC-08")]
    [Trait("Requirement", "CMB-DIS-04")]
    public async Task A_focus_out_close_from_inside_the_popup_touches_and_validates()
    {
        int validations = 0;
        IRenderedComponent<ComboboxFieldHost> cut = test.Context.Render<ComboboxFieldHost>(p => p
            .Add(x => x.ValidationMode, ValidationMode.OnBlur)
            .Add(x => x.Validate, _ =>
            {
                validations++;
                return ValueTask.FromResult<FieldValidationResult?>("error");
            })
            .Add(x => x.Content, builder =>
            {
                builder.OpenComponent<ComboboxTypedHarness<string>>(0);
                builder.AddComponentParameter(1, nameof(ComboboxTypedHarness<string>.RootParameters), new Dictionary<string, object?>
                {
                    ["Items"] = (IReadOnlyList<string>)["one"],
                    ["DefaultOpen"] = true,
                });
                builder.AddComponentParameter(2, nameof(ComboboxTypedHarness<string>.WithInput), false);
                builder.AddComponentParameter(3, nameof(ComboboxTypedHarness<string>.WithTrigger), true);
                builder.CloseComponent();
            }));
        IRenderedComponent<ComboboxTypedHarness<string>> combobox = cut.FindComponent<ComboboxTypedHarness<string>>();
        ComboboxStore store = combobox.Instance.Root!.Store;
        await cut.InvokeAsync(() => cut.Find("[data-testid=trigger]").Focus());

        await cut.InvokeAsync(() => store.RequestOpenAsync(false, new OpenChangeEventDetails(EventReason.FocusOut, null)));

        AngleSharp.Dom.IElement trigger = cut.Find("[data-testid=trigger]");
        Assert.Equal((1, true, false), (validations, trigger.HasAttribute("data-touched"), trigger.HasAttribute("data-focused")));
    }

    [Fact(DisplayName = "Combobox.Label does not label Combobox.Input and warns when input is the form control")]
    [Trait("Requirement", "CMB-STR-10")]
    public void Combobox_Label_does_not_label_the_input_and_warns()
    {
        CapturingLogger<ComboboxLabel> logger = new();
        test.Context.Services.AddSingleton<ILogger<ComboboxLabel>>(logger);

        IRenderedComponent<ComboboxHarness> cut = test.Context.Render<ComboboxHarness>(p => p.Add(x => x.Extra, (RenderFragment)(builder =>
        {
            builder.OpenComponent<ComboboxLabel>(0);
            builder.AddComponentParameter(1, "data-testid", "label");
            builder.CloseComponent();
        })));

        Assert.Single(logger.Entries, entry => entry.Message.StartsWith("Blazix: `<ComboboxLabel>` labels `<ComboboxTrigger>` only.", StringComparison.Ordinal));
        Assert.False(cut.Find("[data-testid=input]").HasAttribute("aria-labelledby"));
    }

    private static RenderFragment Parts(bool label, string? triggerId = null) => builder =>
    {
        if (label)
        {
            builder.OpenComponent<ComboboxLabel>(0);
            builder.AddComponentParameter(1, "data-testid", "label");
            builder.AddComponentParameter(2, "ChildContent", (RenderFragment)(content => content.AddContent(0, "Search")));
            builder.CloseComponent();
        }

        builder.OpenComponent<ComboboxTrigger>(3);
        builder.AddComponentParameter(4, "data-testid", "trigger");
        if (triggerId is not null)
        {
            builder.AddComponentParameter(5, "id", triggerId);
        }

        builder.AddComponentParameter(6, "ChildContent", (RenderFragment)(content => content.AddContent(0, "Open")));
        builder.CloseComponent();
    };

    private static RenderFragment InsidePopup(RenderFragment extra) => builder =>
    {
        builder.OpenComponent<ComboboxHarness>(0);
        builder.AddComponentParameter(1, nameof(ComboboxHarness.StaticItems), (IReadOnlyList<string>)["a", "b"]);
        builder.AddComponentParameter(2, nameof(ComboboxHarness.InputInsidePopup), true);
        builder.AddComponentParameter(3, nameof(ComboboxHarness.Extra), extra);
        builder.CloseComponent();
    };

    private string LabelOptions()
    {
        string? options = null;
        foreach (JSRuntimeInvocation attach in test.Form.Invocations["attach"])
        {
            if (attach.Arguments[1] is string json && json.Contains("\"label\":", StringComparison.Ordinal))
            {
                options = json;
            }
        }

        foreach (JSRuntimeInvocation update in test.FormHandle.Invocations["update"])
        {
            if (update.Arguments[0] is string json && json.Contains("\"label\":", StringComparison.Ordinal))
            {
                options = json;
            }
        }

        return options ?? string.Empty;
    }

    private sealed record Country(string Code, string Label);
}
