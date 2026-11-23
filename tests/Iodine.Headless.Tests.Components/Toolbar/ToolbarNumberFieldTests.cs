using System.Globalization;
using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;

namespace Iodine.Headless.Tests.Components;

// ToolbarInput.test.tsx's "rendering NumberField" titles at L2 (TLB-API-12, TLB-API-14, TLB-DOM-07, TLB-ARIA-04; their
// re-cited NumberField clauses, toolbar.md 2026-10-27 and 2026-11-03); M6 track 6, issue 277, PR 6a. The keyboard halves
// ("handles interactions", the arrows of "disabled state") need the number-field module's input role: 6b, TLB-KEY-03 and
// TLB-KEY-13.
/// <summary>A toolbar input substituted by a number field's input.</summary>
public sealed class ToolbarNumberFieldTests : IDisposable
{
    private const string InteractionsModulePath = "./_content/Iodine.Headless/js/interactions.js";

    private readonly ToolbarTestContext test = new();

    /// <summary>Initializes a new instance of the <see cref="ToolbarNumberFieldTests"/> class.</summary>
    public ToolbarNumberFieldTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.JSInterop.SetupModule(InteractionsModulePath)
            .Setup<string>("readPlatformFlags")
            .SetResult("""{"voiceOver":false,"mac":false,"ios":false}""");
    }

    private BunitContext Context => test.Context;

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "renders NumberField.Input")]
    [Trait("Requirement", "TLB-API-14")]
    public void Renders_NumberField_Input()
    {
        IRenderedComponent<ToolbarRoot> cut = Render(disabled: false);

        Assert.Equal("Number field", cut.Find("input[type=text]").GetAttribute("aria-roledescription"));
    }

    [Fact(DisplayName = "disabled state")]
    [Trait("Requirement", "TLB-API-12")]
    [Trait("Requirement", "TLB-DOM-07")]
    [Trait("Requirement", "TLB-ARIA-04")]
    public void Disabled_state()
    {
        IRenderedComponent<ToolbarRoot> cut = Render(disabled: true);

        IElement input = cut.Find("input[type=text]");
        Assert.False(input.HasAttribute("disabled"));
        Assert.True(input.HasAttribute("data-disabled"));
        Assert.Equal("true", input.GetAttribute("aria-disabled"));
    }

    private IRenderedComponent<ToolbarRoot> Render(bool disabled)
    {
        RenderFragment substitute = builder =>
        {
            builder.OpenComponent<NumberFieldInput>(0);
            builder.CloseComponent();
        };
        RenderFragment group = builder =>
        {
            builder.OpenComponent<NumberFieldDecrement>(0);
            builder.CloseComponent();
            builder.OpenComponent<ToolbarInput>(1);
            builder.AddComponentParameter(2, nameof(ToolbarInput.Disabled), disabled);
            builder.AddComponentParameter(3, nameof(ToolbarInput.Render), substitute);
            builder.CloseComponent();
            builder.OpenComponent<NumberFieldIncrement>(4);
            builder.CloseComponent();
        };
        RenderFragment root = builder =>
        {
            builder.OpenComponent<NumberFieldRoot>(0);
            builder.AddComponentParameter(1, nameof(NumberFieldRoot.Culture), CultureInfo.GetCultureInfo("en-US"));
            builder.AddComponentParameter(2, nameof(NumberFieldRoot.Min), (double?)1);
            builder.AddComponentParameter(3, nameof(NumberFieldRoot.Max), (double?)10);
            builder.AddComponentParameter(4, nameof(NumberFieldRoot.DefaultValue), (double?)5);
            builder.AddComponentParameter(5, nameof(NumberFieldRoot.ChildContent), (RenderFragment)(b =>
            {
                b.OpenComponent<NumberFieldGroup>(0);
                b.AddComponentParameter(1, nameof(NumberFieldGroup.ChildContent), group);
                b.CloseComponent();
            }));
            builder.CloseComponent();
        };

        return Context.Render<ToolbarRoot>(p => p.Add(x => x.ChildContent, root));
    }
}
