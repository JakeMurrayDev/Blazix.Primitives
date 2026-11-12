using System.Globalization;
using AngleSharp.Dom;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

// Family tests use the project's root namespace (tests/Blazix.Primitives.Tests.Components/CONTEXT.md).
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// L2: <see cref="MenuFilterProvider"/>'s matching parameters (MNU-API-54, API-56, I18N-03; M5 track 12), over
/// <see cref="MenuFilterProviderHost"/>. The items' texts reach C# as the List's handle reports them.
/// </summary>
public sealed class MenuFilterProviderTests : BunitContext
{
    private readonly BunitJSModuleInterop module;
    private readonly BunitJSModuleInterop handle;

    public MenuFilterProviderTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlazixPrimitives();
        module = JSInterop.SetupModule("./_content/Blazix.Primitives/js/menu-filter.js");
        handle = module.SetupModule("attach", _ => true);
        handle.SetupVoid("update", _ => true).SetVoidResult();
    }

    [Fact(DisplayName = "applies a custom filter to item text")]
    [Trait("Requirement", "MNU-API-54")]
    public async Task Applies_a_custom_filter()
    {
        List<(string Text, string Query)> calls = [];
        IRenderedComponent<MenuFilterProviderHost> cut = await RenderWithTexts(p => p
            .Add(x => x.FilterMode, "custom")
            .Add(x => x.Filter, (text, query) =>
            {
                calls.Add((text, query));
                return text.StartsWith(query, StringComparison.OrdinalIgnoreCase);
            }));

        await Type(cut, "de");

        Assert.Equal(["Delete"], Items(cut));
        Assert.Contains(("Delete", "de"), calls);
        Assert.Contains(("Rename", "de"), calls);
    }

    [Fact(DisplayName = "keeps every item visible when filtering is turned off with null")]
    [Trait("Requirement", "MNU-API-54")]
    public async Task Keeps_every_item_visible_with_a_null_filter()
    {
        IRenderedComponent<MenuFilterProviderHost> cut = await RenderWithTexts(p => p.Add(x => x.FilterMode, "null"));

        await Type(cut, "zzz");

        Assert.Equal(["Delete", "Rename"], Items(cut));
    }

    [Fact(DisplayName = "uses an updated custom filter function")]
    [Trait("Requirement", "MNU-API-54")]
    [Trait("Requirement", "MNU-STA-15")]
    public async Task Uses_an_updated_filter()
    {
        Func<string, string, bool> startsWith = (text, query) => text.StartsWith(query, StringComparison.OrdinalIgnoreCase);
        Func<string, string, bool> endsWith = (text, query) => text.EndsWith(query, StringComparison.OrdinalIgnoreCase);
        IRenderedComponent<MenuFilterProviderHost> cut = await RenderWithTexts(p => p
            .Add(x => x.FilterMode, "custom")
            .Add(x => x.Filter, startsWith)
            .Add(x => x.Items, ["Apple", "Banana"])
            .Add(x => x.DefaultQuery, "a"));
        Assert.Equal(["Apple"], Items(cut));

        cut.Render(p => p.Add(x => x.Filter, endsWith));

        Assert.Equal(["Banana"], Items(cut));
    }

    [Fact(DisplayName = "uses the configured locale for default matching")]
    [Trait("Requirement", "MNU-API-56")]
    [Trait("Requirement", "MNU-I18N-03")]
    public async Task Uses_the_configured_culture()
    {
        IRenderedComponent<MenuFilterProviderHost> cut = await RenderWithTexts(p => p
            .Add(x => x.Culture, CultureInfo.GetCultureInfo("tr-TR"))
            .Add(x => x.Items, ["Istanbul", "Paris"])
            .Add(x => x.DefaultQuery, "ı"));

        Assert.Equal(["Istanbul"], Items(cut));
    }

    [Fact(DisplayName = "supports a controlled query")]
    [Trait("Requirement", "MNU-API-57")]
    [Trait("Requirement", "MNU-EVT-16")]
    public async Task Supports_a_controlled_query()
    {
        List<string> requests = [];
        IRenderedComponent<MenuFilterProviderHost> cut = await RenderWithTexts(p => p
            .Add(x => x.FilterMode, "controlled")
            .Add(x => x.Items, ["Duplicate", "Rename", "Delete"])
            .Add(x => x.Value, "dup")
            .Add(x => x.OnValueChange, (ValueChangeEventArgs<string> args) => requests.Add(args.Value)));
        Assert.Equal("dup", cut.Find("input").GetAttribute("value"));
        Assert.Equal(["Duplicate"], Items(cut));

        await Type(cut, "dupx");

        // The consumer owns the value, so nothing changes until it updates the parameter; the module writes "dup" back.
        Assert.Equal(["dupx"], requests);
        Assert.Equal(["Duplicate"], Items(cut));
        string update = LastInputUpdate();
        Assert.Contains("\"value\":\"dup\"", update, StringComparison.Ordinal);
        Assert.Contains("\"valueRevision\":1", update, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "port: filters each menu item variant without changing its role")]
    [Trait("Requirement", "MNU-DOM-28")]
    [Trait("Requirement", "MNU-STA-15")]
    public async Task Filters_each_item_variant()
    {
        IRenderedComponent<MenuFilterProviderHost> cut = await RenderWithTexts(p => p
            .Add(x => x.Variants, true)
            .Add(x => x.DefaultQuery, "banana"));

        Assert.Equal(["menuitemcheckbox:Banana"], Roles(cut));
        Assert.False(cut.Find("[role=menuitemcheckbox]").HasAttribute("aria-expanded"));

        await Type(cut, "cherry");
        Assert.Equal(["menuitemradio:Cherry"], Roles(cut));
        Assert.False(cut.Find("[role=menuitemradio]").HasAttribute("aria-expanded"));

        await Type(cut, "date");
        Assert.Equal(["menuitem:Date"], Roles(cut));
        Assert.False(cut.Find("[role=menuitem]").HasAttribute("aria-expanded"));
    }

    private static string[] Roles(IRenderedComponent<MenuFilterProviderHost> cut) =>
        [.. cut.FindAll("[role=menuitem], [role=menuitemcheckbox], [role=menuitemradio]").Select(item => $"{item.GetAttribute("role")}:{item.TextContent.Trim()}")];

    private static string[] Items(IRenderedComponent<MenuFilterProviderHost> cut) =>
        [.. cut.FindAll("[role=menuitem]").Select(item => item.TextContent.Trim())];

    private string LastInputUpdate() =>
        (string)handle.Invocations["update"].Last(i => ((string)i.Arguments[0]!).Contains("\"part\":\"input\"", StringComparison.Ordinal)).Arguments[0]!;

    // The List's report of what each item renders, as the menu-filter module sends it after attach.
    private async Task<IRenderedComponent<MenuFilterProviderHost>> RenderWithTexts(Action<ComponentParameterCollectionBuilder<MenuFilterProviderHost>> parameters)
    {
        IRenderedComponent<MenuFilterProviderHost> cut = Render(parameters);
        IReadOnlyList<IElement> items = cut.FindAll("[role=menuitem], [role=menuitemcheckbox], [role=menuitemradio]");
        string ids = string.Join(",", items.Select(item => $"\"{item.Id}\""));
        string texts = string.Join(",", items.Select(item => $"\"{item.TextContent.Trim()}\""));
        await cut.InvokeAsync(() => Twin("list").OnTexts($$"""{"ids":[{{ids}}],"texts":[{{texts}}]}"""));
        return cut;
    }

    private Task Type(IRenderedComponent<MenuFilterProviderHost> cut, string value) =>
        cut.InvokeAsync(() => Twin("input").OnValueChange($$"""{"value":"{{value}}","reason":"input-change","event":null}"""));

    private MenuFilterTwin Twin(string part) =>
        ((DotNetObjectReference<MenuFilterTwin>)module.Invocations["attach"].Last(i => ((string)i.Arguments[1]!).Contains($"\"part\":\"{part}\"", StringComparison.Ordinal)).Arguments[2]!).Value;
}
