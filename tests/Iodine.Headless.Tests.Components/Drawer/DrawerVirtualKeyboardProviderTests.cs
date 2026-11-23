using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives.Tests.Components;

// M6 track 8c (issue 279): the C# half of Drawer.VirtualKeyboardProvider. The provider renders no element and registers on
// its Root's drawer context; the Viewport tells its `drawer` handle whether one is present, whether the Root is modal (true
// only for `Modal=true`) and how many dialogs are open inside it (DRW-STR-09, API-11, FOC-03, FOC-09; design point 1 (a) of
// 2026-11-18: the keyboard role rides the Viewport's handle). The role itself is the module's (js/drawer.keyboard.test.ts).
public sealed class DrawerVirtualKeyboardProviderTests : IDisposable
{
    private readonly DialogTestContext test = new();
    private readonly BunitJSModuleInterop drawerModule;
    private readonly BunitJSModuleInterop drawerHandle;

    public DrawerVirtualKeyboardProviderTests()
    {
        drawerModule = test.Context.JSInterop.SetupModule("./_content/Blazix.Primitives/js/drawer.js");
        drawerHandle = drawerModule.SetupModule("attach", _ => true);
        drawerHandle.SetupVoid("update", _ => true).SetVoidResult();
        drawerHandle.SetupVoid("dispose", _ => true).SetVoidResult();
    }

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "port: the viewport tells the drawer module whether a VirtualKeyboardProvider is under the root")]
    [Trait("Requirement", "DRW-API-11")]
    [Trait("Requirement", "DRW-STR-09")]
    public void The_viewport_tells_the_module_whether_a_provider_is_present()
    {
        IRenderedComponent<KeyboardHarness> cut = test.Context.Render<KeyboardHarness>();
        cut.WaitForAssertion(() => Assert.Contains("\"keyboard\":false", LastOptions(), StringComparison.Ordinal));

        cut.Render(p => p.Add(x => x.WithProvider, true));
        cut.WaitForAssertion(() => Assert.Contains("\"keyboard\":true", LastOptions(), StringComparison.Ordinal));

        cut.Render(p => p.Add(x => x.WithProvider, false));
        cut.WaitForAssertion(() => Assert.Contains("\"keyboard\":false", LastOptions(), StringComparison.Ordinal));
    }

    [Theory(DisplayName = "port: the viewport sends modal as true only for a modal root")]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("trap-focus", false)]
    [Trait("Requirement", "DRW-FOC-09")]
    public void The_viewport_sends_modal_true_only_for_a_modal_root(string modal, bool expected)
    {
        ModalValue value = modal switch
        {
            "true" => true,
            "false" => false,
            _ => ModalValue.TrapFocus,
        };
        IRenderedComponent<KeyboardHarness> cut = test.Context.Render<KeyboardHarness>(p => p.Add(x => x.WithProvider, true).Add(x => x.Modal, value));

        cut.WaitForAssertion(() => Assert.Contains($"\"modal\":{(expected ? "true" : "false")}", LastOptions(), StringComparison.Ordinal));
    }

    [Fact(DisplayName = "port: the viewport sends the number of dialogs open inside the drawer")]
    [Trait("Requirement", "DRW-FOC-03")]
    [Trait("Requirement", "DRW-FOC-07")]
    public void The_viewport_sends_the_nested_open_dialog_count()
    {
        IRenderedComponent<KeyboardHarness> cut = test.Context.Render<KeyboardHarness>(p => p.Add(x => x.WithProvider, true));
        cut.WaitForAssertion(() => Assert.Contains("\"nestedOpenDialogCount\":0", LastOptions(), StringComparison.Ordinal));

        cut.Render(p => p.Add(x => x.WithNestedDialog, true));
        cut.WaitForAssertion(() => Assert.Contains("\"nestedOpenDialogCount\":1", LastOptions(), StringComparison.Ordinal));
    }

    private List<JSRuntimeInvocation> ViewportAttaches() =>
        drawerModule.Invocations["attach"].Where(attach => IsOuterViewport(attach.Arguments[1])).ToList();

    private List<JSRuntimeInvocation> ViewportUpdates() =>
        drawerHandle.Invocations["update"].Where(update => IsOuterViewport(update.Arguments[0])).ToList();

    // The outer drawer's Viewport: the nested Dialog has no drawer handle, and the outer Viewport is the one not nested.
    private static bool IsOuterViewport(object? options) =>
        options is string json && json.Contains("\"role\":\"viewport\"", StringComparison.Ordinal)
            && json.Contains("\"nested\":false", StringComparison.Ordinal);

    private string LastOptions()
    {
        List<JSRuntimeInvocation> updates = ViewportUpdates();
        return (updates.Count > 0 ? updates[^1].Arguments[0] : ViewportAttaches()[^1].Arguments[1]) as string ?? string.Empty;
    }

    /// <summary>An open drawer whose content is optionally wrapped in a VirtualKeyboardProvider.</summary>
    public sealed class KeyboardHarness : ComponentBase
    {
        /// <summary>Gets or sets a value indicating whether a VirtualKeyboardProvider wraps the drawer's content.</summary>
        [Parameter]
        public bool WithProvider { get; set; }

        /// <summary>Gets or sets a value indicating whether an open Dialog renders inside the drawer's popup.</summary>
        [Parameter]
        public bool WithNestedDialog { get; set; }

        /// <summary>Gets or sets the root's modality.</summary>
        [Parameter]
        public ModalValue? Modal { get; set; }

        /// <inheritdoc />
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<DrawerRoot<NoPayload>>(0);
            builder.AddComponentParameter(1, nameof(DrawerRoot<NoPayload>.DefaultOpen), true);
            if (Modal is { } modal)
            {
                builder.AddComponentParameter(2, nameof(DrawerRoot<NoPayload>.Modal), modal);
            }

            builder.AddComponentParameter(3, nameof(DrawerRoot<NoPayload>.ChildContent), (RenderFragment)(root =>
            {
                if (WithProvider)
                {
                    root.OpenComponent<DrawerVirtualKeyboardProvider>(0);
                    root.AddComponentParameter(1, nameof(DrawerVirtualKeyboardProvider.ChildContent), Parts);
                    root.CloseComponent();
                }
                else
                {
                    root.AddContent(2, Parts);
                }
            }));
            builder.CloseComponent();
        }

        private RenderFragment Parts => builder =>
        {
            builder.OpenComponent<DrawerPortal>(0);
            builder.AddComponentParameter(1, nameof(DrawerPortal.ChildContent), (RenderFragment)(portal =>
            {
                portal.OpenComponent<DrawerViewport>(0);
                portal.AddComponentParameter(1, nameof(DrawerViewport.ChildContent), (RenderFragment)(viewport =>
                {
                    viewport.OpenComponent<DrawerPopup>(0);
                    viewport.AddComponentParameter(1, nameof(DrawerPopup.ChildContent), (RenderFragment)(popup =>
                    {
                        if (WithNestedDialog)
                        {
                            popup.OpenComponent<DialogRoot<NoPayload>>(0);
                            popup.AddComponentParameter(1, nameof(DialogRoot<NoPayload>.DefaultOpen), true);
                            popup.AddComponentParameter(2, nameof(DialogRoot<NoPayload>.ChildContent), (RenderFragment)(dialog =>
                            {
                                dialog.OpenComponent<DialogPortal>(0);
                                dialog.AddComponentParameter(1, nameof(DialogPortal.ChildContent), (RenderFragment)(dialogPortal =>
                                {
                                    dialogPortal.OpenComponent<DialogPopup>(0);
                                    dialogPortal.CloseComponent();
                                }));
                                dialog.CloseComponent();
                            }));
                            popup.CloseComponent();
                        }
                    }));
                    viewport.CloseComponent();
                }));
                portal.CloseComponent();
            }));
            builder.CloseComponent();
        };
    }
}
