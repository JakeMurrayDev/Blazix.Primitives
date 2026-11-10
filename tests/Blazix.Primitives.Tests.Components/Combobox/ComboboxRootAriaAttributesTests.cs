using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// ComboboxRoot.test.tsx's `aria attributes` titles cited at L2. The list navigation writes `aria-activedescendant` and the
// items' `data-highlighted` in the browser (composite.js) and reports the highlight, which C# mirrors as the store's
// ActiveIndex, so a title's activedescendant assertions read that mirror.
/// <summary>Combobox's root at L2: ComboboxRoot.test.tsx's <c>aria attributes</c> block.</summary>
public sealed class ComboboxRootAriaAttributesTests : IDisposable
{
    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "sets all aria attributes on the input when open")]
    [Trait("Requirement", "CMB-DOM-08")]
    [Trait("Requirement", "CMB-ARIA-01")]
    public void Sets_all_aria_attributes_on_the_input_when_open()
    {
        IRenderedComponent<ComboboxRootBatchBHarness> cut = test.Context.Render<ComboboxRootBatchBHarness>(p => p
            .Add(x => x.RootParameters, new Dictionary<string, object?> { ["DefaultOpen"] = true })
            .Add(x => x.StaticItems, []));
        IElement input = cut.Find("[data-testid=input]");
        IElement listbox = cut.Find("[role=listbox]");

        Assert.Equal("combobox", input.GetAttribute("role"));
        Assert.Equal("true", input.GetAttribute("aria-expanded"));
        Assert.Equal("list", input.GetAttribute("aria-autocomplete"));
        Assert.Equal("listbox", input.GetAttribute("aria-haspopup"));
        Assert.Equal(listbox.Id, input.GetAttribute("aria-controls"));
        Assert.False(input.HasAttribute("aria-activedescendant"));
    }

    [Fact(DisplayName = "sets aria-expanded on the input when rendered inline and open")]
    [Trait("Requirement", "CMB-API-23")]
    [Trait("Requirement", "CMB-ARIA-02")]
    public void Sets_aria_expanded_on_the_input_when_rendered_inline_and_open()
    {
        IRenderedComponent<ComboboxRootBatchBHarness> cut = Inline(new() { ["Inline"] = true, ["Open"] = true });
        IElement input = cut.Find("[data-testid=input]");
        IElement listbox = cut.Find("[role=listbox]");

        Assert.Equal("combobox", input.GetAttribute("role"));
        Assert.Equal("true", input.GetAttribute("aria-expanded"));
        Assert.Equal(listbox.Id, input.GetAttribute("aria-controls"));
    }

    [Fact(DisplayName = "sets the popup type on the input when rendered inline as a grid")]
    [Trait("Requirement", "CMB-ARIA-01")]
    public void Sets_the_popup_type_on_the_input_when_rendered_inline_as_a_grid()
    {
        IRenderedComponent<ComboboxRootBatchBHarness> cut = Inline(new() { ["Inline"] = true, ["Open"] = true, ["Grid"] = true });
        IElement input = cut.Find("[data-testid=input]");
        IElement grid = cut.Find("[role=grid]");

        Assert.Equal("true", input.GetAttribute("aria-expanded"));
        Assert.Equal("grid", input.GetAttribute("aria-haspopup"));
        Assert.Equal(grid.Id, input.GetAttribute("aria-controls"));
    }

    [Fact(DisplayName = "keeps the input expanded when rendered inline without the `open` prop")]
    [Trait("Requirement", "CMB-STR-05")]
    [Trait("Requirement", "CMB-ARIA-02")]
    public async Task Keeps_the_input_expanded_when_rendered_inline_without_the_open_prop()
    {
        IRenderedComponent<ComboboxRootBatchBHarness> cut = test.Context.Render<ComboboxRootBatchBHarness>(p => p
            .Add(x => x.RootParameters, new Dictionary<string, object?> { ["Inline"] = true, ["Items"] = (IReadOnlyList<string>)["apple", "banana"] })
            .Add(x => x.Inline, true));
        IElement listbox = cut.Find("[role=listbox]");

        // The list renders regardless of the internal open state, so the input must not claim to be collapsed before it's
        // interacted with.
        Assert.Equal("true", cut.Find("[data-testid=input]").GetAttribute("aria-expanded"));
        Assert.Equal(listbox.Id, cut.Find("[data-testid=input]").GetAttribute("aria-controls"));

        await cut.PressInputAsync(test);

        Assert.Equal("true", cut.Find("[data-testid=input]").GetAttribute("aria-expanded"));
        Assert.Equal(listbox.Id, cut.Find("[data-testid=input]").GetAttribute("aria-controls"));
    }

    [Fact(DisplayName = "sets correct attributes on the item when highlighted")]
    [Trait("Requirement", "CMB-API-59")]
    [Trait("Requirement", "CMB-STA-13")]
    [Trait("Requirement", "CMB-DOM-09")]
    [Trait("Requirement", "CMB-ARIA-03")]
    [Trait("Requirement", "CMB-ARIA-09")]
    public async Task Sets_correct_attributes_on_the_item_when_highlighted()
    {
        IRenderedComponent<ComboboxRootBatchBHarness> cut = test.Context.Render<ComboboxRootBatchBHarness>(p => p
            .Add(x => x.RootParameters, new Dictionary<string, object?> { ["DefaultOpen"] = true })
            .Add(x => x.StaticItems, ["a", "b"]));

        await cut.PressInputAsync(test);

        Assert.Null(cut.Instance.Root!.Store.State.ActiveIndex);

        await cut.HighlightAsync(test, 0, "a");

        Assert.Equal("false", cut.Option("a").GetAttribute("aria-selected"));
        Assert.Equal("false", cut.Option("b").GetAttribute("aria-selected"));
        Assert.Equal(0, cut.Instance.Root.Store.State.ActiveIndex);

        await cut.EnterAsync(test, "a");
        await cut.PressInputAsync(test);

        Assert.Equal("true", cut.Option("a").GetAttribute("aria-selected"));
        Assert.Equal("false", cut.Option("b").GetAttribute("aria-selected"));
    }

    [Fact(DisplayName = "sets aria-controls to the dialog popup on trigger")]
    [Trait("Requirement", "CMB-DOM-24")]
    [Trait("Requirement", "CMB-ARIA-07")]
    public async Task Sets_aria_controls_to_the_dialog_popup_on_trigger()
    {
        IRenderedComponent<ComboboxRootBatchBHarness> cut = InputInside();
        IElement popup = cut.Find("[role=dialog]");

        Assert.NotEqual(string.Empty, popup.Id ?? string.Empty);
        Assert.Equal(popup.Id, Trigger(cut).GetAttribute("aria-controls"));
        Assert.Equal("true", Trigger(cut).GetAttribute("aria-expanded"));

        await cut.PressTriggerAsync(test);

        Assert.Equal("false", Trigger(cut).GetAttribute("aria-expanded"));
        Assert.False(Trigger(cut).HasAttribute("aria-controls"));
    }

    [Fact(DisplayName = "points aria-controls at a custom popup id on the trigger")]
    [Trait("Requirement", "CMB-DOM-24")]
    public void Points_aria_controls_at_a_custom_popup_id_on_the_trigger()
    {
        IRenderedComponent<ComboboxRootBatchBHarness> cut = InputInside(p => p.Add(x => x.PopupAttributes, new Dictionary<string, object> { ["id"] = "custom-popup-id" }));
        IElement popup = cut.Find("[role=dialog]");

        Assert.Equal("custom-popup-id", popup.Id);
        Assert.Equal("custom-popup-id", Trigger(cut).GetAttribute("aria-controls"));
    }

    [Fact(DisplayName = "points aria-controls at a popup id supplied through the render prop")]
    [Trait("Requirement", "CMB-DOM-24")]
    public void Points_aria_controls_at_a_popup_id_supplied_through_the_render_prop()
    {
        RenderFragment element = builder =>
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "id", "custom-render-id");
            builder.CloseElement();
        };
        IRenderedComponent<ComboboxRootBatchBHarness> cut = InputInside(p => p.Add(x => x.PopupRender, element));
        IElement popup = cut.Find("[role=dialog]");

        Assert.Equal("custom-render-id", popup.Id);
        Assert.Equal("custom-render-id", Trigger(cut).GetAttribute("aria-controls"));
    }

    private static IElement Trigger(IRenderedComponent<ComboboxRootBatchBHarness> cut) => cut.Find("[data-testid=trigger]");

    private IRenderedComponent<ComboboxRootBatchBHarness> Inline(Dictionary<string, object?> parameters) =>
        test.Context.Render<ComboboxRootBatchBHarness>(p => p
            .Add(x => x.RootParameters, parameters)
            .Add(x => x.Inline, true)
            .Add(x => x.StaticItems, []));

    private IRenderedComponent<ComboboxRootBatchBHarness> InputInside(Action<ComponentParameterCollectionBuilder<ComboboxRootBatchBHarness>>? more = null) =>
        test.Context.Render<ComboboxRootBatchBHarness>(p =>
        {
            p.Add(x => x.RootParameters, new Dictionary<string, object?> { ["DefaultOpen"] = true })
                .Add(x => x.WithTrigger, true)
                .Add(x => x.InputInsidePopup, true)
                .Add(x => x.StaticItems, []);
            more?.Invoke(p);
        });
}
