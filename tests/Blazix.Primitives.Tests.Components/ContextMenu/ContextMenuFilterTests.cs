using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace Blazix.Primitives.Tests.Components;

// MNU-STR-14 (M5 track 12, issue 197): ContextMenu.SubmenuRoot is Menu.SubmenuRoot (context-menu/index.parts.ts), so a
// MenuFilterProvider around it makes the submenu filterable, as around MenuSubmenuRoot.
/// <summary>ContextMenu's submenu under a filter provider at L2.</summary>
public sealed class ContextMenuFilterTests : BunitContext
{
    /// <summary>Initializes a new instance of the <see cref="ContextMenuFilterTests"/> class.</summary>
    public ContextMenuFilterTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlazixPrimitives();
    }

    [Fact(DisplayName = "port: a context menu's submenu consumes its filter provider: its popup is a dialog holding the input and the list")]
    [Trait("Requirement", "MNU-STR-14")]
    public void A_context_menu_submenu_consumes_its_filter_provider()
    {
        IRenderedComponent<ContextMenuFilterSubmenuHost> cut = Render<ContextMenuFilterSubmenuHost>();

        IElement dialog = cut.Find("[role=dialog]");
        Assert.NotNull(dialog.QuerySelector("[role=searchbox]"));
        Assert.Equal("Documents", Assert.Single(dialog.QuerySelectorAll("[role=menuitem]")).TextContent.Trim());
    }
}
