using AngleSharp.Dom;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

// Family tests use the project's root namespace (tests/Blazix.Primitives.Tests.Components/CONTEXT.md).
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// L2: the items of a filterable menu and <see cref="MenuEmpty"/> (MNU-STA-15, STA-23, API-61, DOM-23, ARIA-16; M5 track
/// 12), over <see cref="MenuFilterItemsHost"/>. Items match by their <c>Label</c> here; their rendered text crosses the
/// boundary through the List's handle (design point 2 (b′)), whose titles come with it.
/// </summary>
public sealed class MenuFilterItemsTests : BunitContext
{
    private readonly BunitJSModuleInterop module;

    public MenuFilterItemsTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlazixPrimitives();
        module = JSInterop.SetupModule("./_content/Blazix.Primitives/js/menu-filter.js");
        module.SetupModule("attach", _ => true).SetupVoid("update", _ => true).SetVoidResult();
    }

    [Fact(DisplayName = "renders Empty when no items are registered at all")]
    [Trait("Requirement", "MNU-STA-23")]
    [Trait("Requirement", "MNU-API-61")]
    [Trait("Requirement", "MNU-DOM-23")]
    [Trait("Requirement", "MNU-ARIA-16")]
    public void Renders_Empty_when_no_items_are_registered()
    {
        IRenderedComponent<MenuFilterItemsHost> cut = Render<MenuFilterItemsHost>(p => p.Add(x => x.Case, "no-items"));

        cut.WaitForAssertion(() =>
        {
            IElement empty = cut.Find("[role=status]");
            Assert.Equal("DIV", empty.TagName);
            Assert.Equal(["polite", "true"], new[] { "aria-live", "aria-atomic" }.Select(empty.GetAttribute));
            Assert.Equal("No actions found", empty.TextContent);
        });
    }

    [Fact(DisplayName = "port: Empty announces its message through a live-region handle each time it appears")]
    [Trait("Requirement", "MNU-DOM-23")]
    public void Empty_attaches_its_live_region()
    {
        BunitJSModuleInterop dom = JSInterop.SetupModule("./_content/Blazix.Primitives/js/dom.js");
        dom.SetupModule("attach", _ => true).SetupVoid("update", _ => true).SetVoidResult();

        IRenderedComponent<MenuFilterItemsHost> cut = Render<MenuFilterItemsHost>(p => p.Add(x => x.Case, "no-items"));

        cut.WaitForAssertion(() =>
        {
            JSRuntimeInvocation attach = Assert.Single(dom.Invocations["attach"], i => ((string)i.Arguments[1]!).Contains("\"liveRegion\":true", StringComparison.Ordinal));
            Assert.Equal(cut.Find("[role=status]").GetAttribute("blazor:elementreference"), ((Microsoft.AspNetCore.Components.ElementReference)attach.Arguments[0]!).Id);
        });
    }

    [Fact(DisplayName = "never mounts the Empty children while items match")]
    [Trait("Requirement", "MNU-API-61")]
    [Trait("Requirement", "MNU-STA-23")]
    public void Never_mounts_Empty_while_items_match()
    {
        int mounts = 0;
        IRenderedComponent<MenuFilterItemsHost> cut = Render<MenuFilterItemsHost>(p => p
            .Add(x => x.Case, "unlabeled")
            .Add(x => x.OnEmptyMount, () => mounts++));

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("[role=menuitem]").Count));
        Assert.Equal(0, mounts);
        Assert.Empty(cut.FindAll("[role=status]"));
    }

    [Fact(DisplayName = "port: hides the items whose label does not match the query, and shows Empty while none does")]
    [Trait("Requirement", "MNU-STA-15")]
    [Trait("Requirement", "MNU-STA-23")]
    [Trait("Requirement", "MNU-API-61")]
    public async Task Hides_the_items_that_do_not_match()
    {
        IRenderedComponent<MenuFilterItemsHost> cut = Render<MenuFilterItemsHost>();
        Assert.Equal(["Rename", "Copy", "Pin", "Delete", "Share"], Items(cut));
        MenuFilterTwin twin = Twin();

        await Type(cut, twin, "e");
        Assert.Equal(["Rename", "Delete", "Share"], Items(cut));
        Assert.Empty(cut.FindAll("[role=status]"));

        await Type(cut, twin, "zzz");
        Assert.Empty(Items(cut));
        cut.WaitForAssertion(() => Assert.Equal("No actions found", cut.Find("[role=status]").TextContent));

        await Type(cut, twin, "ren");
        Assert.Equal(["Rename"], Items(cut));
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[role=status]")));
    }

    [Fact(DisplayName = "port: an item that does not match the first committed query never renders")]
    [Trait("Requirement", "MNU-STA-15")]
    [Trait("Requirement", "MNU-STA-16")]
    public void An_item_that_does_not_match_never_renders()
    {
        IRenderedComponent<MenuFilterItemsHost> cut = Render<MenuFilterItemsHost>(p => p.Add(x => x.DefaultQuery, "co"));

        Assert.Equal(["Copy"], Items(cut));
    }

    [Fact(DisplayName = "port: the list asks for the rendered texts of the items without a label")]
    [Trait("Requirement", "MNU-STA-15")]
    [Trait("Requirement", "MNU-STA-17")]
    public void The_list_asks_for_the_texts_of_unlabeled_items()
    {
        Render<MenuFilterItemsHost>(p => p.Add(x => x.Case, "texts"));

        string options = (string)ListAttach().Arguments[1]!;
        Assert.Contains("\"part\":\"list\"", options, StringComparison.Ordinal);
        Assert.Contains("\"textIds\":[\"rename\",\"copy\"]", options, StringComparison.Ordinal);
        Assert.Matches("\"menuKey\":\"[^\"]+\"", options);
    }

    [Fact(DisplayName = "port: filters items without a label by the text the list reports, visible until it arrives")]
    [Trait("Requirement", "MNU-STA-15")]
    [Trait("Requirement", "MNU-STA-17")]
    public async Task Filters_unlabeled_items_by_their_reported_text()
    {
        IRenderedComponent<MenuFilterItemsHost> cut = Render<MenuFilterItemsHost>(p => p
            .Add(x => x.Case, "texts")
            .Add(x => x.DefaultQuery, "co"));
        Assert.Equal(["Rename", "Copy"], Items(cut));

        MenuFilterTwin list = ((DotNetObjectReference<MenuFilterTwin>)ListAttach().Arguments[2]!).Value;
        await cut.InvokeAsync(() => list.OnTexts("""{"ids":["rename","copy"],"texts":["Rename","Copy"]}"""));
        Assert.Equal(["Copy"], Items(cut));

        await Type(cut, Twin("input"), "ren");
        Assert.Equal(["Rename"], Items(cut));
    }

    private static string[] Items(IRenderedComponent<MenuFilterItemsHost> cut) =>
        [.. cut.FindAll("[role=menuitem], [role=menuitemcheckbox], [role=menuitemradio]").Select(item => item.TextContent.Trim())];

    private static Task Type(IRenderedComponent<MenuFilterItemsHost> cut, MenuFilterTwin twin, string value) =>
        cut.InvokeAsync(() => twin.OnValueChange($$"""{"value":"{{value}}","reason":"input-change","event":null}"""));

    private MenuFilterTwin Twin(string part = "input") =>
        ((DotNetObjectReference<MenuFilterTwin>)module.Invocations["attach"].Last(i => ((string)i.Arguments[1]!).Contains($"\"part\":\"{part}\"", StringComparison.Ordinal)).Arguments[2]!).Value;

    private JSRuntimeInvocation ListAttach() =>
        Assert.Single(module.Invocations["attach"], i => ((string)i.Arguments[1]!).Contains("\"part\":\"list\"", StringComparison.Ordinal));
}
