using AngleSharp.Dom;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

// Family tests use the project's root namespace (tests/Blazix.Primitives.Tests.Components/CONTEXT.md).
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// L2: labels and checkbox state under a filter (MNU-API-36, API-41; M5 track 12), over <see cref="MenuFilterItemKindsHost"/>,
/// <c>MenuFilterRoot.test.tsx</c>'s titles verbatim. The items' texts reach C# as the List's handle reports them; the
/// typing is the Input's report.
/// </summary>
public sealed class MenuFilterItemKindsTests : BunitContext
{
    private readonly BunitJSModuleInterop module;

    public MenuFilterItemKindsTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlazixPrimitives();
        module = JSInterop.SetupModule("./_content/Blazix.Primitives/js/menu-filter.js");
        module.SetupModule("attach", _ => true).SetupVoid("update", _ => true).SetVoidResult();
    }

    [Fact(DisplayName = "prefers the label prop over rendered text for matching")]
    [Trait("Requirement", "MNU-API-36")]
    public async Task Prefers_the_label_over_rendered_text()
    {
        IRenderedComponent<MenuFilterItemKindsHost> cut = Render<MenuFilterItemKindsHost>(p => p.Add(x => x.Case, "label"));
        await ReportTexts(cut);

        await Type(cut, "app");
        cut.WaitForAssertion(() => Assert.Equal(["Fruit one"], MenuItems(cut)));

        await Type(cut, "");
        await Type(cut, "fruit");
        cut.WaitForAssertion(() => Assert.DoesNotContain("Fruit one", MenuItems(cut)));
    }

    [Fact(DisplayName = "matches a submenu trigger on its label")]
    [Trait("Requirement", "MNU-API-36")]
    public async Task Matches_a_submenu_trigger_on_its_label()
    {
        IRenderedComponent<MenuFilterItemKindsHost> cut = Render<MenuFilterItemKindsHost>(p => p.Add(x => x.Case, "submenu-label"));
        await ReportTexts(cut);

        await Type(cut, "directory");

        cut.WaitForAssertion(() => Assert.Equal(["Move to folder"], MenuItems(cut)));
    }

    [Fact(DisplayName = "preserves uncontrolled checkbox state while the item is filtered out")]
    [Trait("Requirement", "MNU-API-41")]
    public async Task Preserves_uncontrolled_checkbox_state()
    {
        IRenderedComponent<MenuFilterItemKindsHost> cut = Render<MenuFilterItemKindsHost>(p => p.Add(x => x.Case, "checkbox"));
        await ReportTexts(cut);
        cut.Find("[role=menuitemcheckbox]").Click();
        cut.WaitForAssertion(() => Assert.Equal("false", cut.Find("[role=menuitemcheckbox]").GetAttribute("aria-checked")));

        await Type(cut, "rename");
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[role=menuitemcheckbox]")));

        await Type(cut, "");

        cut.WaitForAssertion(() => Assert.Equal("false", cut.Find("[role=menuitemcheckbox]").GetAttribute("aria-checked")));
    }

    [Fact(DisplayName = "leaves a checkbox item unchecked when onCheckedChange is canceled")]
    [Trait("Requirement", "MNU-API-42")]
    [Trait("Requirement", "MNU-EVT-08")]
    public void Leaves_a_canceled_checkbox_unchecked()
    {
        IRenderedComponent<MenuFilterItemKindsHost> cut = Render<MenuFilterItemKindsHost>(p => p.Add(x => x.Case, "checkbox-cancel"));

        cut.Find("[role=menuitemcheckbox]").Click();

        Assert.Equal("false", cut.Find("[role=menuitemcheckbox]").GetAttribute("aria-checked"));
    }

    [Fact(DisplayName = "tracks a controlled checkbox item")]
    [Trait("Requirement", "MNU-API-41")]
    [Trait("Requirement", "MNU-EVT-08")]
    public void Tracks_a_controlled_checkbox_item()
    {
        IRenderedComponent<MenuFilterItemKindsHost> cut = Render<MenuFilterItemKindsHost>(p => p.Add(x => x.Case, "checkbox-controlled"));
        Assert.Equal("false", cut.Find("[role=menuitemcheckbox]").GetAttribute("aria-checked"));

        cut.Find("[role=menuitemcheckbox]").Click();

        cut.WaitForAssertion(() => Assert.Equal("true", cut.Find("[role=menuitemcheckbox]").GetAttribute("aria-checked")));
    }

    private static string[] MenuItems(IRenderedComponent<MenuFilterItemKindsHost> cut) =>
        [.. cut.FindAll("[role^=menuitem]").Select(item => item.TextContent.Trim())];

    // The List's report of what its items render.
    private async Task ReportTexts(IRenderedComponent<MenuFilterItemKindsHost> cut)
    {
        IReadOnlyList<IElement> items = cut.FindAll("[role^=menuitem]");
        string ids = string.Join(",", items.Select(item => $"\"{item.Id}\""));
        string texts = string.Join(",", items.Select(item => $"\"{item.TextContent.Trim()}\""));
        foreach (MenuFilterTwin list in Twins("list"))
        {
            await cut.InvokeAsync(() => list.OnTexts($$"""{"ids":[{{ids}}],"texts":[{{texts}}]}"""));
        }
    }

    private Task Type(IRenderedComponent<MenuFilterItemKindsHost> cut, string value) =>
        cut.InvokeAsync(() => Twins("input").First().OnValueChange($$"""{"value":"{{value}}","reason":"input-change","event":null}"""));

    private IEnumerable<MenuFilterTwin> Twins(string part) =>
        module.Invocations["attach"].Where(i => ((string)i.Arguments[1]!).Contains($"\"part\":\"{part}\"", StringComparison.Ordinal)).Select(i => ((DotNetObjectReference<MenuFilterTwin>)i.Arguments[2]!).Value).ToList();
}
