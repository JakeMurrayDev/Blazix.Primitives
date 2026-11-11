using AngleSharp.Dom;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

// Family tests use the project's root namespace (tests/Blazix.Primitives.Tests.Components/CONTEXT.md).
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// L2: groups in a filterable menu (MNU-STA-24, DOM-20; M5 track 12), over <see cref="MenuFilterGroupsHost"/>. The items'
/// texts reach C# as the List's handle reports them (its own half is js/menu-filter.test.ts); the typing is the Input's
/// report.
/// </summary>
public sealed class MenuFilterGroupsTests : BunitContext
{
    private readonly BunitJSModuleInterop module;

    public MenuFilterGroupsTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlazixPrimitives();
        module = JSInterop.SetupModule("./_content/Blazix.Primitives/js/menu-filter.js");
        module.SetupModule("attach", _ => true).SetupVoid("update", _ => true).SetVoidResult();
    }

    [Fact(DisplayName = "hides a group, label included, when the query filters out all of its items")]
    [Trait("Requirement", "MNU-STA-24")]
    [Trait("Requirement", "MNU-DOM-20")]
    public async Task Hides_a_group_whose_items_are_all_filtered_out()
    {
        IRenderedComponent<MenuFilterGroupsHost> cut = await RenderWithTexts("groups");

        await Type(cut, "ren");
        Assert.True(cut.Find("[data-testid=file-group]").HasAttribute("hidden"));
        Assert.False(cut.Find("[data-testid=manage-group]").HasAttribute("hidden"));
        Assert.Equal(["Rename"], Items(cut));

        await Type(cut, "");
        Assert.False(cut.Find("[data-testid=file-group]").HasAttribute("hidden"));
    }

    [Fact(DisplayName = "keeps a group with no registered items visible while filtering")]
    [Trait("Requirement", "MNU-STA-24")]
    public async Task Keeps_a_group_with_no_items_visible()
    {
        IRenderedComponent<MenuFilterGroupsHost> cut = await RenderWithTexts("empty-group");

        await Type(cut, "ren");

        Assert.Equal(["Rename"], Items(cut));
        Assert.False(cut.Find("[data-testid=empty-group]").HasAttribute("hidden"));
    }

    [Fact(DisplayName = "hides a radio group, label included, when the query filters out all of its items")]
    [Trait("Requirement", "MNU-DOM-20")]
    [Trait("Requirement", "MNU-STA-24")]
    public async Task Hides_a_radio_group_whose_items_are_all_filtered_out()
    {
        IRenderedComponent<MenuFilterGroupsHost> cut = await RenderWithTexts("radio");
        Assert.False(cut.Find("[data-testid=sort-group]").HasAttribute("hidden"));

        await Type(cut, "rena");

        Assert.True(cut.Find("[data-testid=sort-group]").HasAttribute("hidden"));
    }

    [Fact(DisplayName = "port: a group of groups is hidden with its inner groups, and shown again with them")]
    [Trait("Requirement", "MNU-STA-24")]
    public async Task Hides_a_group_of_groups()
    {
        IRenderedComponent<MenuFilterGroupsHost> cut = await RenderWithTexts("nested");

        await Type(cut, "ren");
        Assert.True(cut.Find("[data-testid=inner-group]").HasAttribute("hidden"));
        Assert.True(cut.Find("[data-testid=outer-group]").HasAttribute("hidden"));

        await Type(cut, "sa");
        Assert.False(cut.Find("[data-testid=outer-group]").HasAttribute("hidden"));
    }

    private static string[] Items(IRenderedComponent<MenuFilterGroupsHost> cut) =>
        [.. cut.FindAll("[role=menuitem], [role=menuitemradio]").Select(item => item.TextContent.Trim())];

    // The List's report of what each item renders, as the menu-filter module sends it after attach.
    private async Task<IRenderedComponent<MenuFilterGroupsHost>> RenderWithTexts(string @case)
    {
        IRenderedComponent<MenuFilterGroupsHost> cut = Render<MenuFilterGroupsHost>(p => p.Add(x => x.Case, @case));
        IReadOnlyList<IElement> items = cut.FindAll("[role=menuitem], [role=menuitemradio]");
        string ids = string.Join(",", items.Select(item => $"\"{item.Id}\""));
        string texts = string.Join(",", items.Select(item => $"\"{item.TextContent.Trim()}\""));
        await cut.InvokeAsync(() => Twin("list").OnTexts($$"""{"ids":[{{ids}}],"texts":[{{texts}}]}"""));
        return cut;
    }

    private Task Type(IRenderedComponent<MenuFilterGroupsHost> cut, string value) =>
        cut.InvokeAsync(() => Twin("input").OnValueChange($$"""{"value":"{{value}}","reason":"{{(value.Length == 0 ? "input-clear" : "input-change")}}","event":null}"""));

    private MenuFilterTwin Twin(string part) =>
        ((DotNetObjectReference<MenuFilterTwin>)module.Invocations["attach"].Last(i => ((string)i.Arguments[1]!).Contains($"\"part\":\"{part}\"", StringComparison.Ordinal)).Arguments[2]!).Value;
}
