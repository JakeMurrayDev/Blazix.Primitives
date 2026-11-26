using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Iodine.Headless.Tests.Components;

// Issue 238 (M6 track 10, 10f): BTN-EDGE-02's three consequences of composite inference (UBT-STA-01) under a composite
// root, as Toggle infers it (ADR 04 Amendments 2026-10-26): no tabindex of its own (UBT-DOM-05), the button twin attached
// in composite mode so Space activates on keydown (UBT-KEY-12, the module's half at L3), and a disabled native Button with
// the disabled attribute and no aria-disabled (UBT-ARIA-04).
/// <summary>A Button under a composite root infers composite button mode.</summary>
public sealed class ButtonCompositeInferenceTests : IDisposable
{
    private const string ModulePath = "./_content/Iodine.Headless/js/button.js";

    private readonly BunitContext context = new();
    private readonly BunitJSModuleInterop module;

    public ButtonCompositeInferenceTests()
    {
        context.Services.AddIodineHeadless();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        module = context.JSInterop.SetupModule(ModulePath);
        BunitJSModuleInterop handle = module.SetupModule("attach", _ => true);
        handle.SetupVoid("update", _ => true).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
    }

    /// <inheritdoc />
    public void Dispose() => context.Dispose();

    [Fact(DisplayName = "port: a Button under a composite root renders no tabindex of its own, so the item's roving tabindex is the one rendered")]
    [Trait("Requirement", "BTN-EDGE-02")]
    [Trait("Requirement", "UBT-STA-01")]
    [Trait("Requirement", "UBT-DOM-05")]
    public void A_button_under_a_composite_root_renders_no_tabindex_of_its_own()
    {
        IRenderedComponent<TestCompositeRoot> cut = RenderInComposite(disabled: false);

        IElement button = cut.Find("button");
        Assert.Equal("button", button.GetAttribute("type"));
        Assert.False(button.HasAttribute("tabindex"));
    }

    [Fact(DisplayName = "port: a Button under a composite root attaches its button twin in composite mode, so Space activates on keydown")]
    [Trait("Requirement", "BTN-EDGE-02")]
    [Trait("Requirement", "UBT-STA-01")]
    [Trait("Requirement", "UBT-KEY-12")]
    public void A_button_under_a_composite_root_attaches_its_twin_in_composite_mode()
    {
        RenderInComposite(disabled: false);

        string options = (string)Assert.Single(module.Invocations["attach"]).Arguments[1]!;
        Assert.Contains("\"composite\":true", options, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "port: a disabled native Button under a composite root gets the disabled attribute, no aria-disabled and no tabindex")]
    [Trait("Requirement", "BTN-EDGE-02")]
    [Trait("Requirement", "UBT-ARIA-04")]
    [Trait("Requirement", "UBT-DOM-05")]
    public void A_disabled_native_button_under_a_composite_root_gets_disabled_and_no_aria_disabled()
    {
        IRenderedComponent<TestCompositeRoot> cut = RenderInComposite(disabled: true);

        IElement button = cut.Find("button");
        Assert.True(button.HasAttribute("disabled"));
        Assert.False(button.HasAttribute("aria-disabled"));
        Assert.False(button.HasAttribute("tabindex"));
    }

    private IRenderedComponent<TestCompositeRoot> RenderInComposite(bool disabled) =>
        context.Render<TestCompositeRoot>(p => p.AddChildContent(builder =>
        {
            builder.OpenComponent<Button>(0);
            builder.AddComponentParameter(1, nameof(Button.Disabled), disabled);
            builder.AddComponentParameter(2, nameof(Button.ChildContent), (RenderFragment)(b => b.AddContent(0, "Save")));
            builder.CloseComponent();
        }));
}
