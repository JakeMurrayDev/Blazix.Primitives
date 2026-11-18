using System.Globalization;
using AngleSharp.Dom;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// NumberFieldRoot.test.tsx's "Field" and "Form" rows that need no typed text and no blur (issue 277, 6a): the field's
// disabled state, label and description, the filled state at first render, and the control registration. The rows that type
// or blur wait for the input's events (design point 2 of issue 277).
/// <summary>A number field inside a field: its control registration, label, description and the field's state.</summary>
public sealed class NumberFieldFieldTests : IDisposable
{
    private static readonly CultureInfo s_enUs = CultureInfo.GetCultureInfo("en-US");

    private readonly NumberFieldTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "disables the input when disabled=true")]
    [Trait("Requirement", "NUM-API-13")]
    [Trait("Requirement", "NUM-FRM-11")]
    public void Disables_the_input_when_disabled_true()
    {
        IRenderedComponent<FieldRoot> cut = Render(p => p.Add(x => x.Disabled, true));

        Assert.Equal(string.Empty, Input(cut).GetAttribute("disabled"));
        Assert.True(HiddenInput(cut).HasAttribute("disabled"));
        Assert.True(cut.FindComponent<NumberFieldRoot>().Find("div[data-disabled]").HasAttribute("data-disabled"));
    }

    [Fact(DisplayName = "does not disable the input when disabled=false")]
    [Trait("Requirement", "NUM-FRM-11")]
    public void Does_not_disable_the_input_when_disabled_false()
    {
        IRenderedComponent<FieldRoot> cut = Render(p => p.Add(x => x.Disabled, false));

        Assert.Equal("Number field", Input(cut).GetAttribute("aria-roledescription"));
        Assert.False(Input(cut).HasAttribute("disabled"));
    }

    [Fact(DisplayName = "Field.Label")]
    [Trait("Requirement", "NUM-API-18")]
    [Trait("Requirement", "NUM-DOM-09")]
    [Trait("Requirement", "NUM-FRM-10")]
    [Trait("Requirement", "NUM-ARIA-03")]
    public void Field_Label()
    {
        IRenderedComponent<FieldRoot> cut = Render(after: b =>
        {
            b.OpenComponent<FieldLabel>(0);
            b.AddComponentParameter(1, "data-testid", "label");
            b.CloseComponent();
        });

        IElement label = cut.Find("[data-testid=label]");
        Assert.Equal(Input(cut).GetAttribute("id"), label.GetAttribute("for"));
        Assert.Equal(label.GetAttribute("id"), Input(cut).GetAttribute("aria-labelledby"));
    }

    [Fact(DisplayName = "Field.Description")]
    [Trait("Requirement", "NUM-ARIA-04")]
    [Trait("Requirement", "NUM-DOM-15")]
    [Trait("Requirement", "NUM-FRM-10")]
    public void Field_Description()
    {
        IRenderedComponent<FieldRoot> cut = Render(
            input: new Dictionary<string, object> { ["aria-describedby"] = "external-description" },
            after: b =>
            {
                b.OpenComponent<FieldDescription>(0);
                b.AddComponentParameter(1, "data-testid", "description");
                b.CloseComponent();
            });

        string descriptionId = cut.Find("[data-testid=description]").GetAttribute("id")!;
        Assert.Equal($"external-description {descriptionId}", Input(cut).GetAttribute("aria-describedby"));
        Assert.Equal(descriptionId, HiddenInput(cut).GetAttribute("aria-describedby"));
    }

    // The first half of "has [data-filled] attribute when already filled"; the clearing half types (design point 2).
    [Fact(DisplayName = "has [data-filled] attribute when already filled")]
    [Trait("Requirement", "NUM-STA-13")]
    [Trait("Requirement", "NUM-DOM-04")]
    public void Has_data_filled_attribute_when_already_filled()
    {
        IRenderedComponent<FieldRoot> cut = Render(defaultValue: 1);

        Assert.True(Input(cut).HasAttribute("data-filled"));
        Assert.True(cut.Find("div[data-filled]").HasAttribute("data-filled"));
    }

    // NUM-FRM-06, NUM-FRM-01: the visible input registers as the field's control; the hidden input is the validation input,
    // owned by the `form` module from attach with the serialized value, and hands its focus to the visible input.
    [Fact(DisplayName = "port: the hidden input is the field's validation input and the visible input its control")]
    [Trait("Requirement", "NUM-FRM-01")]
    [Trait("Requirement", "NUM-FRM-06")]
    [Trait("Requirement", "NUM-ARIA-10")]
    public async Task The_hidden_input_is_the_fields_validation_input_and_the_visible_input_its_control()
    {
        IRenderedComponent<FieldRoot> cut = Render(defaultValue: 1.5);

        FormControlOptions attach = test.FormOptions[0];
        Assert.True(attach.Controlled);
        Assert.Equal("1.5", attach.Value);
        Assert.False(attach.ReportEvents);
        Assert.Equal(Input(cut).GetAttribute("id"), attach.FocusTarget);

        await test.ClickAsync(cut, cut.FindComponent<NumberFieldIncrement>().Instance);

        Assert.Equal("2.5", test.FormOptions[^1].Value);
        Assert.True(Input(cut).HasAttribute("data-dirty"));
    }

    private IRenderedComponent<FieldRoot> Render(
        Action<ComponentParameterCollectionBuilder<FieldRoot>>? field = null,
        double? defaultValue = null,
        IReadOnlyDictionary<string, object>? input = null,
        RenderFragment? after = null)
    {
        RenderFragment parts = b =>
        {
            b.OpenComponent<NumberFieldInput>(0);
            if (input is not null)
            {
                b.AddMultipleAttributes(1, input);
            }

            b.CloseComponent();
            b.OpenComponent<NumberFieldIncrement>(2);
            b.CloseComponent();
        };

        return test.Context.Render<FieldRoot>(p =>
        {
            field?.Invoke(p);
            p.Add(x => x.ChildContent, (RenderFragment)(b =>
            {
                b.OpenComponent<NumberFieldRoot>(0);
                b.AddComponentParameter(1, nameof(NumberFieldRoot.Culture), s_enUs);
                if (defaultValue is not null)
                {
                    b.AddComponentParameter(2, nameof(NumberFieldRoot.DefaultValue), defaultValue);
                }

                b.AddComponentParameter(3, nameof(NumberFieldRoot.ChildContent), parts);
                b.CloseComponent();
                b.AddContent(4, after);
            }));
        });
    }

    private static IElement Input(IRenderedComponent<FieldRoot> cut) => cut.Find("input[type=text]");

    private static IElement HiddenInput(IRenderedComponent<FieldRoot> cut) => cut.Find("input[aria-hidden][type=number]");
}
