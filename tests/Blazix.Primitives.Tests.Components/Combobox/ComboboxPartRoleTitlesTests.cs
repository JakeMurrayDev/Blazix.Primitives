using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// PR 193 delta review B1: Base UI's titles for what C# renders on the Popup (ComboboxPopup.test.tsx), the List
// (ComboboxList.test.tsx), the Chips and Chip (ComboboxChips.test.tsx, ComboboxChip.test.tsx) and the Input's aria-required
// (ComboboxInput.test.tsx), at L2.
/// <summary>Combobox's part roles and their ARIA at L2.</summary>
public sealed class ComboboxPartRoleTitlesTests : IDisposable
{
    private static readonly IReadOnlyList<string> s_items = ["apple", "banana"];

    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "sets role to presentation when input renders outside the popup")]
    [Trait("Requirement", "CMB-ARIA-11")]
    [Trait("Requirement", "CMB-STR-03")]
    public void Sets_role_to_presentation_when_input_renders_outside_the_popup()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["DefaultOpen"] = true }, insidePopup: false);

        Assert.Equal("presentation", cut.Find("[data-testid=popup]").GetAttribute("role"));
    }

    [Fact(DisplayName = "sets role to dialog when input renders inside the popup")]
    [Trait("Requirement", "CMB-ARIA-11")]
    [Trait("Requirement", "CMB-STR-03")]
    public void Sets_role_to_dialog_when_input_renders_inside_the_popup()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["DefaultOpen"] = true }, insidePopup: true);

        Assert.Equal("dialog", cut.Find("[data-testid=popup]").GetAttribute("role"));
    }

    [Fact(DisplayName = "sets role=listbox and aria-multiselectable in multiple mode")]
    [Trait("Requirement", "CMB-ARIA-08")]
    public void Sets_role_listbox_and_aria_multiselectable_in_multiple_mode()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["DefaultOpen"] = true, ["Multiple"] = true });

        Assert.Equal("listbox", cut.Find("[data-testid=list]").GetAttribute("role"));
        Assert.Equal("true", cut.Find("[data-testid=list]").GetAttribute("aria-multiselectable"));
    }

    [Fact(DisplayName = "sets aria-readonly on the listbox when the root is readOnly")]
    [Trait("Requirement", "CMB-ARIA-08")]
    public void Sets_aria_readonly_on_the_listbox_when_the_root_is_readOnly()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["DefaultOpen"] = true, ["ReadOnly"] = true });

        Assert.Equal("true", cut.Find("[data-testid=list]").GetAttribute("aria-readonly"));
    }

    [Fact(DisplayName = "does not set aria-readonly on the grid when the root is readOnly")]
    [Trait("Requirement", "CMB-ARIA-08")]
    public void Does_not_set_aria_readonly_on_the_grid_when_the_root_is_readOnly()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["DefaultOpen"] = true, ["ReadOnly"] = true, ["Grid"] = true });

        Assert.Equal("grid", cut.Find("[data-testid=list]").GetAttribute("role"));
        Assert.False(cut.Find("[data-testid=list]").HasAttribute("aria-readonly"));
    }

    [Fact(DisplayName = "does not set aria-readonly on the listbox by default")]
    [Trait("Requirement", "CMB-ARIA-08")]
    public void Does_not_set_aria_readonly_on_the_listbox_by_default()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["DefaultOpen"] = true });

        Assert.False(cut.Find("[data-testid=list]").HasAttribute("aria-readonly"));
    }

    [Fact(DisplayName = "sets aria-required attribute when required")]
    [Trait("Requirement", "CMB-API-28")]
    [Trait("Requirement", "CMB-ARIA-01")]
    public void Sets_aria_required_attribute_when_required()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["Required"] = true });

        Assert.Equal("true", cut.Find("[data-testid=input]").GetAttribute("aria-required"));
    }

    [Fact(DisplayName = "does not set role=\"toolbar\" when there are no chips")]
    [Trait("Requirement", "CMB-ARIA-12")]
    public void Does_not_set_role_toolbar_when_there_are_no_chips()
    {
        IRenderedComponent<ComboboxHarness> cut = RenderChips(new() { ["Multiple"] = true }, []);

        Assert.False(cut.Find("[data-testid=chips]").HasAttribute("role"));
    }

    [Fact(DisplayName = "sets role=\"toolbar\" when there is at least one chip")]
    [Trait("Requirement", "CMB-ARIA-12")]
    [Trait("Requirement", "CMB-STA-34")]
    public void Sets_role_toolbar_when_there_is_at_least_one_chip()
    {
        IRenderedComponent<ComboboxHarness> cut = RenderChips(new() { ["Multiple"] = true, ["DefaultValues"] = (IReadOnlyList<string>)["apple"] }, ["apple"]);

        Assert.Equal("toolbar", cut.Find("[data-testid=chips]").GetAttribute("role"));
    }

    [Fact(DisplayName = "should render aria-readonly attribute when readOnly")]
    [Trait("Requirement", "CMB-ARIA-12")]
    public void Should_render_aria_readonly_attribute_when_readOnly()
    {
        IRenderedComponent<ComboboxHarness> cut = RenderChips(new() { ["Multiple"] = true, ["ReadOnly"] = true, ["DefaultValues"] = (IReadOnlyList<string>)["apple"] }, ["apple"]);

        Assert.Equal("true", cut.Find("[data-testid=chip-apple]").GetAttribute("aria-readonly"));
    }

    // ComboboxEmpty.tsx:27-46 (CMB-DOM-23): the element always renders, so its live region persists; its children only while
    // the filtered list is empty.
    [Fact(DisplayName = "renders when there are no filtered items")]
    [Trait("Requirement", "CMB-DOM-23")]
    public void Renders_when_there_are_no_filtered_items()
    {
        IRenderedComponent<ComboboxHarness> cut = RenderEmpty(new() { ["DefaultOpen"] = true, ["Items"] = (IReadOnlyList<string>)[] });

        Assert.Contains("No items.", cut.Find("[data-testid=empty]").TextContent, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "does not render when there are items")]
    [Trait("Requirement", "CMB-DOM-23")]
    public void Does_not_render_when_there_are_items()
    {
        IRenderedComponent<ComboboxHarness> cut = RenderEmpty(new() { ["DefaultOpen"] = true });

        Assert.DoesNotContain("No items.", cut.Markup, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "renders when the search query matches no items")]
    [Trait("Requirement", "CMB-DOM-23")]
    public void Renders_when_the_search_query_matches_no_items()
    {
        IRenderedComponent<ComboboxHarness> cut = RenderEmpty(new() { ["DefaultOpen"] = true, ["DefaultInputValue"] = "d" });

        Assert.Contains("No items.", cut.Find("[data-testid=empty]").TextContent, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "does not render when the search query matches an item")]
    [Trait("Requirement", "CMB-DOM-23")]
    public void Does_not_render_when_the_search_query_matches_an_item()
    {
        IRenderedComponent<ComboboxHarness> cut = RenderEmpty(new() { ["DefaultOpen"] = true, ["DefaultInputValue"] = "banana" });

        Assert.DoesNotContain("No items.", cut.Markup, StringComparison.Ordinal);
    }

    // ComboboxStatus.tsx:22-36 (CMB-DOM-23): rendered inside the Popup, so it exists only while the popup is mounted.
    [Fact(DisplayName = "renders only when open")]
    [Trait("Requirement", "CMB-DOM-23")]
    public async Task Renders_only_when_open()
    {
        void Status(RenderTreeBuilder builder)
        {
            builder.OpenComponent<ComboboxStatus>(0);
            builder.AddComponentParameter(1, "data-testid", "status");
            builder.AddComponentParameter(2, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Searching…")));
            builder.CloseComponent();
        }

        IRenderedComponent<ComboboxHarness> cut = test.Context.Render<ComboboxHarness>(p => p
            .Add(x => x.RootParameters, new Dictionary<string, object?> { ["Items"] = s_items })
            .Add(x => x.PopupExtra, (RenderFragment)Status));
        Assert.Empty(cut.FindAll("[data-testid=status]"));

        await test.PressAsync(cut, "[data-testid=input]", "input-press");

        Assert.Single(cut.FindAll("[data-testid=status]"));
    }

    private IRenderedComponent<ComboboxHarness> RenderEmpty(Dictionary<string, object?> root)
    {
        root.TryAdd("Items", s_items);
        return test.Context.Render<ComboboxHarness>(p => p.Add(x => x.RootParameters, root).Add(x => x.WithEmpty, true));
    }

    private IRenderedComponent<ComboboxHarness> Render(Dictionary<string, object?> root, bool insidePopup = false)
    {
        root.TryAdd("Items", s_items);
        return test.Context.Render<ComboboxHarness>(p => p.Add(x => x.RootParameters, root).Add(x => x.InputInsidePopup, insidePopup));
    }

    private IRenderedComponent<ComboboxHarness> RenderChips(Dictionary<string, object?> root, IReadOnlyList<string> chips)
    {
        void Extra(RenderTreeBuilder builder)
        {
            builder.OpenComponent<ComboboxChips>(0);
            builder.AddComponentParameter(1, "data-testid", "chips");
            builder.AddComponentParameter(2, "ChildContent", (RenderFragment)(inner =>
            {
                foreach (string value in chips)
                {
                    inner.OpenComponent<ComboboxChip>(0);
                    inner.SetKey(value);
                    inner.AddComponentParameter(1, "data-testid", "chip-" + value);
                    inner.AddComponentParameter(2, "ChildContent", (RenderFragment)(chip => chip.AddContent(0, value)));
                    inner.CloseComponent();
                }
            }));
            builder.CloseComponent();
        }

        return test.Context.Render<ComboboxHarness>(p => p.Add(x => x.RootParameters, root).Add(x => x.Extra, (RenderFragment)Extra));
    }
}
