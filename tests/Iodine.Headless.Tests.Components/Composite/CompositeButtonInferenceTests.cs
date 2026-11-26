using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Iodine.Headless.Tests.Components;

// Issue 107 (M6 track 10, 10f): UBT-STA-01's inference on the button-like parts Base UI's useButton infers it on
// (useButton.ts:25-26), as Toggle and Button infer it (ADR 04 Amendments 2026-10-26; issue 238): under a composite root the
// part's button twin attaches in composite mode (Space activates on keydown, UBT-KEY-12) and the part renders no tabindex
// of its own (UBT-DOM-05). RadioRoot stays explicit `false`, as Base UI passes it.
/// <summary>The button-like parts infer composite button mode under a composite root.</summary>
public sealed class CompositeButtonInferenceTests : IDisposable
{
    private const string ButtonModulePath = "./_content/Iodine.Headless/js/button.js";

    private readonly BunitContext context = new();
    private readonly BunitJSModuleInterop module;

    public CompositeButtonInferenceTests()
    {
        context.Services.AddIodineHeadless();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        module = context.JSInterop.SetupModule(ButtonModulePath);
        BunitJSModuleInterop handle = module.SetupModule("attach", _ => true);
        handle.SetupVoid("update", _ => true).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
    }

    /// <inheritdoc />
    public void Dispose() => context.Dispose();

    [Fact(DisplayName = "port: a SwitchRoot under a composite root infers composite button mode: no tabindex of its own, and its button twin attaches in composite mode")]
    [Trait("Requirement", "UBT-STA-01")]
    [Trait("Requirement", "UBT-DOM-05")]
    [Trait("Requirement", "UBT-KEY-12")]
    public void A_switch_root_under_a_composite_root_infers_composite_button_mode()
    {
        IRenderedComponent<TestCompositeRoot> cut = RenderInComposite(builder =>
        {
            builder.OpenComponent<SwitchRoot>(0);
            builder.CloseComponent();
        });

        AssertComposite(cut.Find("[role=switch]"));
    }

    [Fact(DisplayName = "port: a CheckboxRoot under a composite root infers composite button mode: no tabindex of its own, and its button twin attaches in composite mode")]
    [Trait("Requirement", "UBT-STA-01")]
    [Trait("Requirement", "UBT-DOM-05")]
    [Trait("Requirement", "UBT-KEY-12")]
    public void A_checkbox_root_under_a_composite_root_infers_composite_button_mode()
    {
        IRenderedComponent<TestCompositeRoot> cut = RenderInComposite(builder =>
        {
            builder.OpenComponent<CheckboxRoot>(0);
            builder.CloseComponent();
        });

        AssertComposite(cut.Find("[role=checkbox]"));
    }

    private IRenderedComponent<TestCompositeRoot> RenderInComposite(RenderFragment content) =>
        context.Render<TestCompositeRoot>(p => p.AddChildContent(content));

    private void AssertComposite(IElement element)
    {
        Assert.False(element.HasAttribute("tabindex"));
        string options = (string)Assert.Single(module.Invocations["attach"]).Arguments[1]!;
        Assert.Contains("\"composite\":true", options, StringComparison.Ordinal);
    }
}
