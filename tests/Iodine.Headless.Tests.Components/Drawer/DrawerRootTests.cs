using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Family tests use the project's root namespace (tests/Iodine.Headless.Tests.Components/CONTEXT.md).
namespace Iodine.Headless.Tests.Components;

/// <summary>
/// The <c>[L2]</c> titles of <c>DrawerRoot.test.tsx</c> that need only the root, its triggers, its Close and its handle
/// (titles verbatim; DRW-STR-01..03, API-12, DOM-16, ARIA-03, EDGE-02), and the port titles of the handle's type separation
/// (DRW-API-12) and the keyboard provider's shape (DRW-STR-09). A trigger press is the <c>interactions</c> module's report,
/// driven into the trigger's twin as on Server; a close's exit ends when the test completes the popup's transition. The
/// snap-point, swipe and CloseWatcher titles wait for the <c>drawer</c> module (8b, 8c).
/// </summary>
public sealed class DrawerRootTests : IDisposable
{
    private const string MissingRoot = "Iodine: The `DrawerRoot` context is missing. Drawer parts must be placed within `<DrawerRoot>`.";

    private readonly DialogTestContext test = new();
    private readonly CapturingLogger<DrawerRoot<int?>> numberLogger = new();

    public DrawerRootTests()
    {
        test.Context.Services.AddSingleton<ILogger<DrawerRoot<int?>>>(numberLogger);
    }

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    public static TheoryData<string> DrawerRootConsumers => ["Content", "VirtualKeyboardProvider", "Popup", "Viewport", "SwipeArea"];

    [Fact(DisplayName = "exposes the attributes rendered by borrowed trigger and close parts")]
    [Trait("Requirement", "DRW-STR-01")]
    [Trait("Requirement", "DRW-STR-02")]
    [Trait("Requirement", "DRW-STR-03")]
    [Trait("Requirement", "DLG-DOM-03")]
    [Trait("Requirement", "DLG-DOM-04")]
    [Trait("Requirement", "DLG-DOM-24")]
    public async Task Exposes_the_attributes_rendered_by_borrowed_trigger_and_close_parts()
    {
        IRenderedComponent<DrawerRootHarness> cut = test.Context.Render<DrawerRootHarness>(p => p.Add(x => x.Modal, (ModalValue?)false));

        Assert.True(cut.Find("#disabled").HasAttribute(DrawerTriggerDataAttributes.Disabled));
        Assert.True(cut.Find("[data-testid=close]").HasAttribute(DrawerCloseDataAttributes.Disabled));

        IElement trigger = cut.Find("#open");
        Assert.False(trigger.HasAttribute(DrawerTriggerDataAttributes.PopupOpen));
        await test.PressAsync(cut, "open", open: true);
        Assert.True(cut.Find("#open").HasAttribute(DrawerTriggerDataAttributes.PopupOpen));
    }

    [Fact(DisplayName = "supports detached triggers with handles")]
    [Trait("Requirement", "DRW-STR-01")]
    [Trait("Requirement", "DRW-API-12")]
    [Trait("Requirement", "DLG-API-13")]
    [Trait("Requirement", "DLG-ARIA-02")]
    public async Task Supports_detached_triggers_with_handles()
    {
        IRenderedComponent<DrawerDetachedHarness> cut = test.Context.Render<DrawerDetachedHarness>();
        Assert.Empty(cut.FindAll("[data-testid=payload]"));

        await test.PressAsync(cut, "trigger-1", open: true);
        Assert.Equal("1", cut.Find("[data-testid=payload]").TextContent);

        await cut.Find("[data-testid=close]").ClickAsync(new MouseEventArgs { Detail = 1 });
        await test.CompletePopupTransitionAsync(cut, open: false);
        Assert.Empty(cut.FindAll("[data-testid=payload]"));

        await test.PressAsync(cut, "trigger-2", open: true);
        Assert.Equal("2", cut.Find("[data-testid=payload]").TextContent);
    }

    // The bystander's renders are counted through its ClassValue (Base UI counts its render function's calls).
    [Fact(DisplayName = "does not re-render inactive detached triggers when the popup opens and closes")]
    [Trait("Requirement", "DRW-STR-01")]
    [Trait("Requirement", "DLG-EDGE-16")]
    public async Task Does_not_re_render_inactive_detached_triggers_when_the_popup_opens_and_closes()
    {
        int bystander = 0;
        IRenderedComponent<DrawerInactiveTriggersHarness> cut = test.Context.Render<DrawerInactiveTriggersHarness>(p => p
            .Add(x => x.BystanderClassValue, _ =>
            {
                bystander++;
                return null;
            }));
        Assert.True(bystander > 0);

        bystander = 0;
        await OpenAndCloseAsync();
        await OpenAndCloseAsync();
        Assert.Equal(0, bystander);

        async Task OpenAndCloseAsync()
        {
            await test.PressAsync(cut, "trigger-1", open: true);
            Assert.Single(cut.FindAll("[data-testid=popup]"));
            Assert.Equal("true", cut.Find("#trigger-1").GetAttribute("aria-expanded"));
            await cut.Find("[data-testid=close]").ClickAsync(new MouseEventArgs { Detail = 1 });
            await test.CompletePopupTransitionAsync(cut, open: false);
            Assert.Empty(cut.FindAll("[data-testid=popup]"));
            Assert.Equal("false", cut.Find("#trigger-1").GetAttribute("aria-expanded"));
        }
    }

    [Fact(DisplayName = "supports imperative actions with handles")]
    [Trait("Requirement", "DRW-STR-01")]
    [Trait("Requirement", "DRW-API-12")]
    [Trait("Requirement", "DRW-ARIA-03")]
    [Trait("Requirement", "DLG-API-24")]
    [Trait("Requirement", "DLG-API-25")]
    [Trait("Requirement", "DLG-API-26")]
    [Trait("Requirement", "DLG-ARIA-02")]
    [Trait("Requirement", "DLG-ARIA-03")]
    public async Task Supports_imperative_actions_with_handles()
    {
        IRenderedComponent<DrawerDetachedHarness> cut = test.Context.Render<DrawerDetachedHarness>();
        DrawerHandle<int?> handle = cut.Instance.Handle;
        Assert.Empty(cut.FindAll("[role=dialog]"));

        await cut.InvokeAsync(() => handle.OpenAsync("trigger-2"));
        IElement dialog = cut.Find("[role=dialog]");
        Assert.Equal("2", cut.Find("[data-testid=payload]").TextContent);
        Assert.Equal("true", cut.Find("#trigger-2").GetAttribute("aria-expanded"));
        Assert.Equal(dialog.Id, cut.Find("#trigger-2").GetAttribute("aria-controls"));
        Assert.Equal("false", cut.Find("#trigger-1").GetAttribute("aria-expanded"));

        await cut.InvokeAsync(() => handle.CloseAsync());
        await test.CompletePopupTransitionAsync(cut, open: false);
        Assert.Empty(cut.FindAll("[role=dialog]"));
        Assert.Equal("false", cut.Find("#trigger-2").GetAttribute("aria-expanded"));

        await cut.InvokeAsync(() => handle.OpenWithPayloadAsync(8));
        Assert.Single(cut.FindAll("[role=dialog]"));
        Assert.Equal("8", cut.Find("[data-testid=payload]").TextContent);
        Assert.Equal("false", cut.Find("#trigger-1").GetAttribute("aria-expanded"));
        Assert.Equal("false", cut.Find("#trigger-2").GetAttribute("aria-expanded"));
    }

    [Fact(DisplayName = "attaches fresh root state when a handle-backed root remounts")]
    [Trait("Requirement", "DRW-STR-01")]
    [Trait("Requirement", "DRW-API-12")]
    [Trait("Requirement", "DLG-EDGE-04")]
    [Trait("Requirement", "DLG-EDGE-05")]
    [Trait("Requirement", "DLG-EDGE-12")]
    public async Task Attaches_fresh_root_state_when_a_handle_backed_root_remounts()
    {
        IRenderedComponent<DrawerRemountHarness> cut = test.Context.Render<DrawerRemountHarness>();
        DrawerHandle<int?> handle = cut.Instance.Handle;
        Assert.Equal("No payload", cut.Find("[data-testid=payload]").TextContent);

        await test.PressAsync(cut, "trigger", open: true);
        Assert.Contains("Drawer content", cut.Find("[role=dialog]").TextContent, StringComparison.Ordinal);
        Assert.Equal("1", cut.Find("[data-testid=payload]").TextContent);
        Assert.Equal("true", cut.Find("#trigger").GetAttribute("aria-expanded"));
        Assert.Equal(cut.Find("[role=dialog]").Id, cut.Find("#trigger").GetAttribute("aria-controls"));

        await cut.Find("[data-testid=unmount]").ClickAsync(new MouseEventArgs());
        Assert.False(handle.IsOpen);
        Assert.Empty(cut.FindAll("[role=dialog]"));

        await cut.InvokeAsync(async () =>
        {
            await handle.OpenWithPayloadAsync(8);
            await handle.OpenAsync("trigger");
            await handle.CloseAsync();
        });
        Assert.False(handle.IsOpen);
        Assert.Equal(3, numberLogger.Entries.Count(entry => entry.Message.Contains("no root using this handle is mounted", StringComparison.Ordinal)));

        await cut.Find("[data-testid=remount]").ClickAsync(new MouseEventArgs());
        Assert.Equal("No payload", cut.Find("[data-testid=payload]").TextContent);
        Assert.Empty(cut.FindAll("[role=dialog]"));
        Assert.Equal("false", cut.Find("#trigger").GetAttribute("aria-expanded"));
        Assert.False(cut.Find("#trigger").HasAttribute("aria-controls"));

        await test.PressAsync(cut, "trigger", open: true);
        Assert.Contains("Drawer content", cut.Find("[role=dialog]").TextContent, StringComparison.Ordinal);
        Assert.Equal("1", cut.Find("[data-testid=payload]").TextContent);
        Assert.Equal("true", cut.Find("#trigger").GetAttribute("aria-expanded"));
        Assert.Equal(cut.Find("[role=dialog]").Id, cut.Find("#trigger").GetAttribute("aria-controls"));
    }

    [Fact(DisplayName = "synchronizes trigger aria-controls with the popup id")]
    [Trait("Requirement", "DRW-STR-01")]
    [Trait("Requirement", "DRW-DOM-16")]
    [Trait("Requirement", "DRW-ARIA-03")]
    public async Task Synchronizes_trigger_aria_controls_with_the_popup_id()
    {
        IRenderedComponent<DrawerRootHarness> cut = test.Context.Render<DrawerRootHarness>(p => p.Add(x => x.WithPopup, true));
        Assert.Equal("dialog", cut.Find("#open").GetAttribute("aria-haspopup"));

        await test.PressAsync(cut, "open", open: true);

        IElement popup = cut.Find("[data-testid=popup]");
        Assert.Equal("dialog", popup.GetAttribute("role"));
        Assert.False(string.IsNullOrEmpty(popup.Id));
        Assert.Equal(popup.Id, cut.Find("#open").GetAttribute("aria-controls"));
        Assert.Equal("true", cut.Find("#open").GetAttribute("aria-expanded"));
    }

    // Base UI's MissingRootContextConsumer reads useDrawerRootContext alone; the port's parts that read the Drawer root's own
    // state are Content, VirtualKeyboardProvider, Popup, Viewport and SwipeArea (the last three under a Dialog root, so only
    // the Drawer context is missing; DrawerViewport.tsx:100 and DrawerSwipeArea.tsx:89 read useDrawerRootContext()).
    [Theory(DisplayName = "throws a descriptive error when the root context is missing")]
    [MemberData(nameof(DrawerRootConsumers))]
    [Trait("Requirement", "DRW-EDGE-02")]
    [Trait("Requirement", "DRW-STR-05")]
    [Trait("Requirement", "DRW-STR-06")]
    [Trait("Requirement", "DRW-STR-09")]
    [Trait("Requirement", "DRW-STR-07")]
    public void Throws_a_descriptive_error_when_the_root_context_is_missing(string part)
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => test.Context.Render(Consumer(part)));
        Assert.Equal(MissingRoot, error.Message);
    }

    // DRW-STR-09's own test file is Chromium-only; its shape is C#'s: no element of its own, the content rendered as is.
    [Fact(DisplayName = "port: a DrawerVirtualKeyboardProvider renders no element of its own, only its content")]
    [Trait("Requirement", "DRW-STR-09")]
    public void A_DrawerVirtualKeyboardProvider_renders_no_element_of_its_own()
    {
        IRenderedComponent<IComponent> cut = test.Context.Render(builder =>
        {
            builder.OpenComponent<DrawerRoot<NoPayload>>(0);
            builder.AddComponentParameter(1, nameof(DrawerRoot<NoPayload>.ChildContent), (RenderFragment)(root =>
            {
                root.OpenComponent<DrawerVirtualKeyboardProvider>(0);
                root.AddComponentParameter(1, nameof(DrawerVirtualKeyboardProvider.ChildContent), (RenderFragment)(content =>
                {
                    content.OpenElement(0, "span");
                    content.AddAttribute(1, "data-testid", "inside");
                    content.CloseElement();
                }));
                root.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Assert.NotNull(cut.Find("[data-testid=inside]"));
        Assert.Equal("<span data-testid=\"inside\"></span>", cut.Markup.Trim());
    }

    // DRW §14, ADL-EDGE-02's form: C#'s nominal types separate the families one way. A Drawer root or trigger takes a
    // DrawerHandle, which a DialogHandle or an AlertDialogHandle is not; a Dialog root accepts a DrawerHandle.
    [Fact(DisplayName = "port: the Drawer parts take a DrawerHandle, which a DialogHandle is not, and the Dialog parts accept one")]
    [Trait("Requirement", "DRW-API-12")]
    public void The_Drawer_parts_take_a_DrawerHandle()
    {
        Assert.Equal(typeof(DrawerHandle<int>), typeof(DrawerRoot<int>).GetProperty("Handle")?.PropertyType);
        Assert.Equal(typeof(DrawerHandle<int>), typeof(DrawerTrigger<int>).GetProperty("Handle")?.PropertyType);
        Assert.False(typeof(DrawerHandle<int>).IsAssignableFrom(typeof(DialogHandle<int>)));
        Assert.False(typeof(DrawerHandle<int>).IsAssignableFrom(typeof(AlertDialogHandle<int>)));

        Assert.Equal(typeof(DialogHandle<int>), typeof(DialogRoot<int>).GetProperty("Handle")?.PropertyType);
        Assert.True(typeof(DialogHandle<int>).IsAssignableFrom(typeof(DrawerHandle<int>)));
        Assert.Equal(typeof(DialogHandle<int>), typeof(DrawerHandle<int>).BaseType);
    }

    private static RenderFragment Consumer(string part) => builder =>
    {
        switch (part)
        {
            case "Content":
                builder.OpenComponent<DrawerContent>(0);
                builder.CloseComponent();
                break;
            case "VirtualKeyboardProvider":
                builder.OpenComponent<DrawerVirtualKeyboardProvider>(0);
                builder.CloseComponent();
                break;
            default:
                builder.OpenComponent<DialogRoot<NoPayload>>(0);
                builder.AddComponentParameter(1, nameof(DialogRoot<NoPayload>.Open), true);
                builder.AddComponentParameter(2, nameof(DialogRoot<NoPayload>.ChildContent), (RenderFragment)(root =>
                {
                    if (part == "SwipeArea")
                    {
                        root.OpenComponent<DrawerSwipeArea>(2);
                        root.CloseComponent();
                        return;
                    }

                    root.OpenComponent<DialogPortal>(0);
                    root.AddComponentParameter(1, nameof(DialogPortal.ChildContent), (RenderFragment)(portal =>
                    {
                        if (part == "Viewport")
                        {
                            portal.OpenComponent<DrawerViewport>(0);
                            portal.CloseComponent();
                        }
                        else
                        {
                            portal.OpenComponent<DrawerPopup>(1);
                            portal.CloseComponent();
                        }
                    }));
                    root.CloseComponent();
                }));
                builder.CloseComponent();
                break;
        }
    };
}
