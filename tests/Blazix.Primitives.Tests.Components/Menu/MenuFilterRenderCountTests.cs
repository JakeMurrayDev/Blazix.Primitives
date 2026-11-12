using AngleSharp.Dom;
using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

// Family tests use the project's root namespace (tests/Blazix.Primitives.Tests.Components/CONTEXT.md).
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// ADR 16 rule 2 for Menu's filter mode (M5 track 12, the final review's fix 6): which parts one filtering keystroke renders,
/// counted through each part's <c>ClassValue</c>. An open filterable menu holds an input, a clear, an empty state and a list
/// of twenty items, ten "Kilo" and ten "Mike"; the query "o" hides the ten "Mike" items. The items' texts reach C# as the
/// List's handle reports them, and the keystroke is the Input's report.
/// </summary>
public sealed class MenuFilterRenderCountTests : BunitContext
{
    private readonly BunitJSModuleInterop module;
    private readonly Dictionary<string, int> renders = [];

    public MenuFilterRenderCountTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlazixPrimitives();
        module = JSInterop.SetupModule("./_content/Blazix.Primitives/js/menu-filter.js");
        module.SetupModule("attach", _ => true).SetupVoid("update", _ => true).SetVoidResult();
    }

    // Base UI has no render-count title; React commits the keystroke's changes once. Hiding an item removes it from the
    // popup's composite list, whose root renders the popup to send the new items; the step renders the popup once, not once
    // per hidden item: every visible item and the List once, the Input and the Clear for the query and for that render, the
    // hidden items and the Empty (which shows nothing while items match) not at all.
    [Fact(DisplayName = "port: one filtering keystroke renders the popup's parts once for the items it hides, not once per hidden item")]
    [Trait("Requirement", "ADR16-2")]
    [Trait("Requirement", "MNU-STA-15")]
    public async Task One_filtering_keystroke_renders_the_popups_parts_once()
    {
        IRenderedComponent<MenuFilterProvider> cut = Render<MenuFilterProvider>(p => p.Add(x => x.ChildContent, (RenderFragment)Menu));
        await ReportTexts(cut);
        renders.Clear();

        await cut.InvokeAsync(() => Twins("input").First().OnValueChange("""{"value":"o","reason":"input-change","event":null}"""));

        cut.WaitForAssertion(() => Assert.Equal(10, cut.FindAll("[role=menuitem]").Count));
        string rendered = string.Join(' ', renders.Keys.Order(StringComparer.Ordinal).Select(key => renders[key] == 1 ? key : key + "×" + renders[key]));
        Assert.Equal("clear×2 input×2 " + string.Join(' ', Enumerable.Range(1, 10).Select(i => $"kilo-{i:00}")) + " list", rendered);
    }

    private void Menu(RenderTreeBuilder builder)
    {
        builder.OpenComponent<MenuRoot<NoPayload>>(0);
        builder.AddComponentParameter(1, nameof(MenuRoot<NoPayload>.DefaultOpen), true);
        builder.AddComponentParameter(2, nameof(MenuRoot<NoPayload>.ChildContent), (RenderFragment)(root =>
        {
            root.OpenComponent<MenuTrigger<NoPayload>>(0);
            root.CloseComponent();
            root.OpenComponent<MenuPortal>(1);
            root.AddComponentParameter(2, nameof(MenuPortal.ChildContent), (RenderFragment)(portal =>
            {
                portal.OpenComponent<MenuPositioner>(0);
                portal.AddComponentParameter(1, nameof(MenuPositioner.ChildContent), (RenderFragment)(positioner =>
                {
                    positioner.OpenComponent<MenuPopup>(0);
                    positioner.AddComponentParameter(1, nameof(MenuPopup.ChildContent), (RenderFragment)Popup);
                    positioner.CloseComponent();
                }));
                portal.CloseComponent();
            }));
            root.CloseComponent();
        }));
        builder.CloseComponent();
    }

    private void Popup(RenderTreeBuilder builder)
    {
        builder.OpenComponent<MenuInput>(0);
        builder.AddAttribute(1, "aria-label", "Filter actions");
        builder.AddComponentParameter(2, nameof(MenuInput.ClassValue), Count<MenuInputState>("input"));
        builder.CloseComponent();
        builder.OpenComponent<MenuClear>(3);
        builder.AddComponentParameter(4, nameof(MenuClear.ClassValue), Count<MenuClearState>("clear"));
        builder.CloseComponent();
        builder.OpenComponent<MenuEmpty>(5);
        builder.AddComponentParameter(6, nameof(MenuEmpty.ClassValue), Count<EmptyState>("empty"));
        builder.AddComponentParameter(7, nameof(MenuEmpty.ChildContent), (RenderFragment)(empty => empty.AddContent(0, "No actions found")));
        builder.CloseComponent();
        builder.OpenComponent<MenuList>(8);
        builder.AddComponentParameter(9, nameof(MenuList.ClassValue), Count<EmptyState>("list"));
        builder.AddComponentParameter(10, nameof(MenuList.ChildContent), (RenderFragment)Items);
        builder.CloseComponent();
    }

    private void Items(RenderTreeBuilder builder)
    {
        foreach (string word in new[] { "Kilo", "Mike" })
        {
            for (int i = 1; i <= 10; i++)
            {
                string key = $"{word.ToLowerInvariant()}-{i:00}";
                builder.OpenComponent<MenuItem>(0);
                builder.SetKey(key);
                builder.AddComponentParameter(1, nameof(MenuItem.ClassValue), Count<MenuItemState>(key));
                builder.AddComponentParameter(2, nameof(MenuItem.ChildContent), (RenderFragment)(item => item.AddContent(0, $"{word} {i}")));
                builder.CloseComponent();
            }
        }
    }

    private Func<TState, string?> Count<TState>(string key) => _ =>
    {
        renders[key] = renders.GetValueOrDefault(key) + 1;
        return null;
    };

    // The List's report of what its items render.
    private async Task ReportTexts(IRenderedComponent<MenuFilterProvider> cut)
    {
        IReadOnlyList<IElement> items = cut.FindAll("[role=menuitem]");
        string ids = string.Join(",", items.Select(item => $"\"{item.Id}\""));
        string texts = string.Join(",", items.Select(item => $"\"{item.TextContent.Trim()}\""));
        foreach (MenuFilterTwin list in Twins("list"))
        {
            await cut.InvokeAsync(() => list.OnTexts($$"""{"ids":[{{ids}}],"texts":[{{texts}}]}"""));
        }
    }

    private IEnumerable<MenuFilterTwin> Twins(string part) =>
        module.Invocations["attach"].Where(i => ((string)i.Arguments[1]!).Contains($"\"part\":\"{part}\"", StringComparison.Ordinal)).Select(i => ((DotNetObjectReference<MenuFilterTwin>)i.Arguments[2]!).Value).ToList();
}
