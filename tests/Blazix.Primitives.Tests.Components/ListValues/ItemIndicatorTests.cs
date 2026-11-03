using AngleSharp.Dom;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The item indicator behaviour at L2 (RND-API-12, DOM-50, DOM-51, ARIA-01, ANI-01, ANI-02) on the test-only indicator, with
/// SelectItemIndicator.test.tsx's and ComboboxItemIndicator.test.tsx's titles (the families are incidental, ADR 13
/// Amendments 2026-10-22; the shells re-run them). The <c>transitions</c> module is faked: its exit report is driven here,
/// and <c>data-ending-style</c> is the module's to write, so C# never renders it.
/// </summary>
public sealed class ItemIndicatorTests : IDisposable
{
    private const string TransitionsModulePath = "./_content/Blazix.Primitives/js/transitions.js";

    private readonly BunitContext context = new();
    private readonly BunitJSModuleInterop transitions;

    public ItemIndicatorTests()
    {
        context.Services.AddBlazixPrimitives();
        transitions = context.JSInterop.SetupModule(TransitionsModulePath);
        BunitJSModuleInterop handle = transitions.SetupModule("attach", _ => true);
        handle.SetupVoid("update", _ => true).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
    }

    /// <inheritdoc />
    public void Dispose() => context.Dispose();

    [Fact(DisplayName = "mounts only while selected when keepMounted is false")]
    [Trait("Requirement", "RND-ANI-01")]
    [Trait("Requirement", "RND-ARIA-01")]
    [Trait("Requirement", "RND-DOM-51")]
    public async Task Mounts_only_while_selected_when_keepMounted_is_false()
    {
        IRenderedComponent<TestItemIndicator> cut = context.Render<TestItemIndicator>(p => p.Add(x => x.Selected, false));
        Assert.Empty(cut.FindAll("span"));

        cut.Render(p => p.Add(x => x.Selected, true));

        IElement indicator = cut.Find("span");
        Assert.True(indicator.HasAttribute("data-selected"));
        Assert.Equal("true", indicator.GetAttribute("aria-hidden"));
        Assert.False(indicator.HasAttribute("selected"));

        cut.Render(p => p.Add(x => x.Selected, false));
        await CompleteExitAsync(cut);
        Assert.Empty(cut.FindAll("span"));

        cut.Render(p => p.Add(x => x.Selected, true));
        Assert.True(cut.Find("span").HasAttribute("data-selected"));
    }

    [Fact(DisplayName = "preserves the kept-mounted element across selection changes")]
    [Trait("Requirement", "RND-ANI-02")]
    [Trait("Requirement", "RND-API-12")]
    public async Task Preserves_the_kept_mounted_element_across_selection_changes()
    {
        IRenderedComponent<TestItemIndicator> cut = context.Render<TestItemIndicator>(p => p.Add(x => x.Selected, false).Add(x => x.KeepMounted, true));
        IElement indicator = cut.Find("span");
        Assert.False(indicator.HasAttribute("data-selected"));

        cut.Render(p => p.Add(x => x.Selected, true));
        Assert.Same(indicator, cut.Find("span"));
        Assert.True(indicator.HasAttribute("data-selected"));

        cut.Render(p => p.Add(x => x.Selected, false));
        await CompleteExitAsync(cut);
        Assert.Same(indicator, cut.Find("span"));
        Assert.False(indicator.HasAttribute("data-selected"));
        Assert.False(indicator.HasAttribute("data-ending-style"));

        cut.Render(p => p.Add(x => x.Selected, true));
        Assert.Same(indicator, cut.Find("span"));
        Assert.True(indicator.HasAttribute("data-selected"));
    }

    [Fact(DisplayName = "updates a mounted indicator when its item becomes unselected")]
    [Trait("Requirement", "RND-DOM-51")]
    public void Updates_a_mounted_indicator_when_its_item_becomes_unselected()
    {
        IRenderedComponent<TestItemIndicator> cut = context.Render<TestItemIndicator>(p => p.Add(x => x.Selected, true).Add(x => x.KeepMounted, true));
        Assert.True(cut.Find("span").HasAttribute("data-selected"));

        cut.Render(p => p.Add(x => x.Selected, false));

        Assert.False(cut.Find("span").HasAttribute("data-selected"));
    }

    // RND-DOM-50 (Test: none upstream): the default content is a heavy check mark, which ChildContent replaces.
    [Fact(DisplayName = "port: the indicator renders a check mark by default, and its child content instead when given")]
    [Trait("Requirement", "RND-DOM-50")]
    public void Renders_a_check_mark_by_default_and_its_child_content_instead_when_given()
    {
        IRenderedComponent<TestItemIndicator> cut = context.Render<TestItemIndicator>(p => p.Add(x => x.Selected, true));
        Assert.Equal("✔️", cut.Find("span").TextContent);

        IRenderedComponent<TestItemIndicator> custom = context.Render<TestItemIndicator>(p => p.Add(x => x.Selected, true).AddChildContent("<b>on</b>"));
        Assert.Equal("on", custom.Find("span").TextContent);
    }

    [Fact(DisplayName = "port: an indicator selected at mount carries no starting phase, and one selection creates hands data-starting-style to the module")]
    [Trait("Requirement", "RND-ANI-01")]
    public void An_indicator_selected_at_mount_carries_no_starting_phase()
    {
        IRenderedComponent<TestItemIndicator> mountedSelected = context.Render<TestItemIndicator>(p => p.Add(x => x.Selected, true));
        Assert.False(mountedSelected.Find("span").HasAttribute("data-starting-style"));

        IRenderedComponent<TestItemIndicator> cut = context.Render<TestItemIndicator>(p => p.Add(x => x.Selected, false));
        cut.Render(p => p.Add(x => x.Selected, true));
        Assert.True(cut.Find("span").HasAttribute("data-starting-style"));
    }

    private Task CompleteExitAsync(IRenderedComponent<TestItemIndicator> cut)
    {
        TransitionsTwin twin = ((DotNetObjectReference<TransitionsTwin>)transitions.Invocations["attach"][^1].Arguments[2]!).Value;
        return cut.InvokeAsync(() => twin.OnTransitionComplete("""{"open":false}"""));
    }
}
