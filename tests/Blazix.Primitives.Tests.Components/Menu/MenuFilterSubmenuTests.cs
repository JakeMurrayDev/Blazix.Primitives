using AngleSharp.Dom;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

// Family tests use the project's root namespace (tests/Blazix.Primitives.Tests.Components/CONTEXT.md).
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// L2: a submenu trigger under a filterable parent (MNU-STA-25; M5 track 12), over <see cref="MenuFilterSubmenuHost"/>. The
/// items' texts reach C# as the parent List's handle reports them; the typing is the parent Input's report. In bUnit a
/// closing popup stays mounted (no transitions module), so the titles read the close request, not the removal.
/// </summary>
public sealed class MenuFilterSubmenuTests : BunitContext
{
    private readonly BunitJSModuleInterop module;

    public MenuFilterSubmenuTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlazixPrimitives();
        module = JSInterop.SetupModule("./_content/Blazix.Primitives/js/menu-filter.js");
        module.SetupModule("attach", _ => true).SetupVoid("update", _ => true).SetVoidResult();
    }

    [Fact(DisplayName = "port: closes the submenu when the query filters out its trigger")]
    [Trait("Requirement", "MNU-STA-25")]
    public async Task Closes_the_submenu_when_its_trigger_is_filtered_out()
    {
        List<(bool Open, EventReason Reason)> requests = [];
        IRenderedComponent<MenuFilterSubmenuHost> cut = Render<MenuFilterSubmenuHost>(p => p
            .Add(x => x.OnSubmenuOpenChange, (open, reason) => requests.Add((open, reason))));
        await ReportTexts(cut);

        await Type(cut, "rename");

        cut.WaitForAssertion(() => Assert.Contains((false, EventReason.None), requests));
    }

    [Fact(DisplayName = "port: keeps an open submenu trigger mounted when its filter close is canceled")]
    [Trait("Requirement", "MNU-STA-25")]
    public async Task Keeps_the_trigger_when_the_close_is_canceled()
    {
        List<(bool Open, EventReason Reason)> requests = [];
        IRenderedComponent<MenuFilterSubmenuHost> cut = Render<MenuFilterSubmenuHost>(p => p
            .Add(x => x.CancelSubmenuClose, true)
            .Add(x => x.OnSubmenuOpenChange, (open, reason) => requests.Add((open, reason))));
        await ReportTexts(cut);

        await Type(cut, "rename");

        cut.WaitForAssertion(() => Assert.Contains((false, EventReason.None), requests));
        Assert.Single(cut.FindAll("[role=menuitem]"), item => item.TextContent.Trim() == "Move to folder");
        Assert.Equal("true", cut.FindAll("[role=menuitem]").Single(item => item.TextContent.Trim() == "Move to folder").GetAttribute("aria-expanded"));
    }

    // The keyboard step of Base UI's title (ArrowDown after typing) is the module's (L4); the filtering is C#'s.
    [Fact(DisplayName = "filters a non-filterable submenu trigger from a filterable parent")]
    [Trait("Requirement", "MNU-STA-25")]
    public async Task Filters_a_non_filterable_submenu_trigger()
    {
        IRenderedComponent<MenuFilterSubmenuTriggerHost> cut = Render<MenuFilterSubmenuTriggerHost>(p => p.Add(x => x.FilterableSubmenu, true));
        await ReportTexts(cut);

        await Type(cut, "rename");

        cut.WaitForAssertion(() => Assert.Equal(["Rename"], MenuItems(cut)));
    }

    [Fact(DisplayName = "filters a Menu.SubmenuTrigger used inside a plain submenu root")]
    [Trait("Requirement", "MNU-STA-25")]
    public async Task Filters_a_submenu_trigger_of_a_plain_submenu_root()
    {
        IRenderedComponent<MenuFilterSubmenuTriggerHost> cut = Render<MenuFilterSubmenuTriggerHost>();
        await ReportTexts(cut);

        await Type(cut, "rename");
        cut.WaitForAssertion(() => Assert.Equal(["Rename"], MenuItems(cut)));

        await Type(cut, "");
        await Type(cut, "move");

        cut.WaitForAssertion(() => Assert.Equal(["Move to folder"], MenuItems(cut)));
    }

    private static string[] MenuItems<THost>(IRenderedComponent<THost> cut)
        where THost : IComponent =>
        [.. cut.FindAll("[role=menuitem]").Select(item => item.TextContent.Trim())];

    // The parent list's report of what its items render.
    private async Task ReportTexts<THost>(IRenderedComponent<THost> cut)
        where THost : IComponent
    {
        IReadOnlyList<IElement> items = cut.FindAll("[role=menuitem]");
        string ids = string.Join(",", items.Select(item => $"\"{item.Id}\""));
        string texts = string.Join(",", items.Select(item => $"\"{item.TextContent.Trim()}\""));
        foreach (MenuFilterTwin list in Twins("list"))
        {
            await cut.InvokeAsync(() => list.OnTexts($$"""{"ids":[{{ids}}],"texts":[{{texts}}]}"""));
        }
    }

    // The parent menu's input, attached first.
    private Task Type<THost>(IRenderedComponent<THost> cut, string value)
        where THost : IComponent =>
        cut.InvokeAsync(() => Twins("input").First().OnValueChange($$"""{"value":"{{value}}","reason":"input-change","event":null}"""));

    private IEnumerable<MenuFilterTwin> Twins(string part) =>
        module.Invocations["attach"].Where(i => ((string)i.Arguments[1]!).Contains($"\"part\":\"{part}\"", StringComparison.Ordinal)).Select(i => ((DotNetObjectReference<MenuFilterTwin>)i.Arguments[2]!).Value).ToList();
}
