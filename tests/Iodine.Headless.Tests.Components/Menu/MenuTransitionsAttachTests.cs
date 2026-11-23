using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

// Family tests use the project's root namespace (tests/Blazix.Primitives.Tests.Components/CONTEXT.md).
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// L2, issue 226 (issue 213's Menu half): what only a transitions update carries, the popup's style and an unmount request,
/// reaches the module when it changed while the popup's or the backdrop's handle was attaching.
/// </summary>
public sealed class MenuTransitionsAttachTests : BunitContext
{
    private const string PopupOptions = "\"reportOpenComplete\":true";
    private const string BackdropOptions = "\"reportOpenComplete\":false";

    public MenuTransitionsAttachTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlazixPrimitives();
    }

    // The held runtime is a test double, not interop: the analyzer reads its members as JS interop calls.
#pragma warning disable BL0016

    // Issue 213's shape (PR 215 fixed it in Popover, Tooltip, PreviewCard and Dialog): a render during a pending transitions
    // attach awaited it and sent nothing, so what only an update carries waited for the next render. The open state is not
    // lost: the module reads it from the element through the open selector.
    [Fact(DisplayName = "port: a style change while the popup's transitions handle attaches reaches the module once it attaches")]
    [Trait("Requirement", "MNU-ANI-12")]
    public async Task A_style_change_while_the_popups_handle_attaches_reaches_the_module()
    {
        BunitJSModuleInterop handle = SetUpTransitions();
        HeldAttachRuntime runtime = HoldTransitionsAttach();
        IRenderedComponent<MenuTransitionsAttachHost> cut = Render<MenuTransitionsAttachHost>(p => p.Add(x => x.PopupStyle, "color: red"));
        await runtime.WaitForHeldAttachAsync(options => options.Contains(PopupOptions, StringComparison.Ordinal) && options.Contains("color: red", StringComparison.Ordinal));

        cut.Render(p => p.Add(x => x.PopupStyle, "color: blue"));
        runtime.Release();

        Assert.True(await HeldAttachRuntime.SentUpdateAsync(Renderer.Dispatcher, handle, options => options.Contains(PopupOptions, StringComparison.Ordinal) && options.Contains("color: blue", StringComparison.Ordinal)));
    }

    [Fact(DisplayName = "port: an unmount requested while the popup's transitions handle attaches reaches the module once it attaches")]
    [Trait("Requirement", "MNU-ANI-10")]
    public async Task An_unmount_while_the_popups_handle_attaches_reaches_the_module()
    {
        await AssertUnmountReachesTheModuleAsync(PopupOptions);
    }

    [Fact(DisplayName = "port: an unmount requested while the backdrop's transitions handle attaches reaches the module once it attaches")]
    [Trait("Requirement", "MNU-ANI-10")]
    [Trait("Requirement", "MNU-ANI-11")]
    public async Task An_unmount_while_the_backdrops_handle_attaches_reaches_the_module()
    {
        await AssertUnmountReachesTheModuleAsync(BackdropOptions);
    }

    // A retained close, then the consumer's unmount, both before the part's attach returns.
    private async Task AssertUnmountReachesTheModuleAsync(string partOptions)
    {
        BunitJSModuleInterop handle = SetUpTransitions();
        HeldAttachRuntime runtime = HoldTransitionsAttach();
        IRenderedComponent<MenuTransitionsAttachHost> cut = Render<MenuTransitionsAttachHost>();
        await runtime.WaitForHeldAttachAsync(options => options.Contains(partOptions, StringComparison.Ordinal));

        MenuRoot<NoPayload> root = cut.Instance.Root!;
        await root.CloseAsync();
        root.Unmount();
        runtime.Release();

        Assert.True(await HeldAttachRuntime.SentUpdateAsync(
            Renderer.Dispatcher,
            handle,
            options => options.Contains(partOptions, StringComparison.Ordinal) && options.Contains("\"unmountRequests\":1", StringComparison.Ordinal)));
    }

    private BunitJSModuleInterop SetUpTransitions()
    {
        BunitJSModuleInterop transitions = JSInterop.SetupModule("./_content/Blazix.Primitives/js/transitions.js");
        BunitJSModuleInterop handle = transitions.SetupModule("attach", _ => true);
        handle.SetupVoid("update", _ => true).SetVoidResult();
        return handle;
    }

    private HeldAttachRuntime HoldTransitionsAttach()
    {
        HeldAttachRuntime runtime = new(JSInterop.JSRuntime, "transitions.js");
        Services.AddSingleton<IJSRuntime>(runtime);
        return runtime;
    }

#pragma warning restore BL0016
}
