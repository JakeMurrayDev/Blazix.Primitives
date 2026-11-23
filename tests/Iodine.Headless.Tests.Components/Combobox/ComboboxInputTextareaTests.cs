using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// Base UI's ComboboxInput.test.tsx titles for an Input rendered as a <textarea> (CMB-DOM-01, CMB-ARIA-05), at L2: the
// substitute is the part's Render fragment.
/// <summary>Combobox's Input rendered as a textarea at L2.</summary>
public sealed class ComboboxInputTextareaTests : IDisposable
{
    private static readonly IReadOnlyList<string> s_items = ["apple", "banana"];

    private static readonly RenderFragment s_textarea = builder =>
    {
        builder.OpenElement(0, "textarea");
        builder.CloseElement();
    };

    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "renders as a <textarea> without an invalid `type` attribute and stays editable")]
    [Trait("Requirement", "CMB-DOM-01")]
    public async Task Renders_as_a_textarea_without_an_invalid_type_attribute_and_stays_editable()
    {
        IRenderedComponent<ComboboxHarness> cut = RenderPopup();

        AngleSharp.Dom.IElement input = cut.Find("[data-testid=input]");
        Assert.Equal("TEXTAREA", input.TagName);
        Assert.False(input.HasAttribute("type"));
        Assert.False(input.HasAttribute("readonly"));
        Assert.False(input.HasAttribute("disabled"));

        await test.TypeAsync(cut, "app");

        Assert.Equal("app", cut.Instance.Root!.Store.State.InputValue);
        Assert.Contains("\"value\":\"app\"", test.LastInputOptions(), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "applies combobox aria attributes to a <textarea> only while the popup is open")]
    [Trait("Requirement", "CMB-ARIA-05")]
    public async Task Applies_combobox_aria_attributes_to_a_textarea_only_while_the_popup_is_open()
    {
        IRenderedComponent<ComboboxHarness> cut = RenderPopup();

        // Closed: combobox semantics are not exposed on a non-input control.
        AngleSharp.Dom.IElement input = cut.Find("[data-testid=input]");
        Assert.NotEqual("combobox", input.GetAttribute("role"));
        Assert.False(input.HasAttribute("aria-expanded"));
        Assert.False(input.HasAttribute("aria-controls"));

        await test.TypeAsync(cut, "a");
        string listId = cut.Find("[role=listbox]").Id!;

        // Open: the combobox ARIA contract is applied to the textarea.
        input = cut.Find("[data-testid=input]");
        Assert.Equal("combobox", input.GetAttribute("role"));
        Assert.Equal("true", input.GetAttribute("aria-expanded"));
        Assert.Equal(listId, input.GetAttribute("aria-controls"));
    }

    [Fact(DisplayName = "applies combobox aria attributes to an inline <textarea> before it is interacted with")]
    [Trait("Requirement", "CMB-ARIA-05")]
    public void Applies_combobox_aria_attributes_to_an_inline_textarea_before_it_is_interacted_with()
    {
        void Content(RenderTreeBuilder builder)
        {
            builder.OpenComponent<ComboboxRoot<string>>(0);
            builder.AddComponentParameter(1, nameof(ComboboxRoot<string>.Inline), true);
            builder.AddComponentParameter(2, nameof(ComboboxRoot<string>.Items), s_items);
            builder.AddComponentParameter(3, nameof(ComboboxRoot<string>.ChildContent), (RenderFragment)(inner =>
            {
                inner.OpenComponent<ComboboxInput>(0);
                inner.AddComponentParameter(1, "data-testid", "input");
                inner.AddComponentParameter(2, nameof(ComboboxInput.Render), s_textarea);
                inner.CloseComponent();
                inner.OpenComponent<ComboboxList<string>>(3);
                inner.AddComponentParameter(4, nameof(ComboboxList<string>.ItemContent), (RenderFragment<ComboboxListItem<string>>)(item => itemBuilder =>
                {
                    itemBuilder.OpenComponent<ComboboxItem<string>>(0);
                    itemBuilder.AddComponentParameter(1, nameof(ComboboxItem<string>.Value), item.Value);
                    itemBuilder.SetKey(item.Value);
                    itemBuilder.AddComponentParameter(2, "ChildContent", (RenderFragment)(text => text.AddContent(0, item.Value)));
                    itemBuilder.CloseComponent();
                }));
                inner.CloseComponent();
            }));
            builder.CloseComponent();
        }

        IRenderedComponent<IComponent> cut = test.Context.Render((RenderFragment)Content);

        AngleSharp.Dom.IElement input = cut.Find("[data-testid=input]");
        string listId = cut.Find("[role=listbox]").Id!;
        Assert.Equal("combobox", input.GetAttribute("role"));
        Assert.Equal("true", input.GetAttribute("aria-expanded"));
        Assert.Equal(listId, input.GetAttribute("aria-controls"));
        Assert.Equal("listbox", input.GetAttribute("aria-haspopup"));
        Assert.Equal("list", input.GetAttribute("aria-autocomplete"));
    }

    private IRenderedComponent<ComboboxHarness> RenderPopup()
    {
        void Extra(RenderTreeBuilder builder)
        {
            builder.OpenComponent<ComboboxInput>(0);
            builder.AddComponentParameter(1, "data-testid", "input");
            builder.AddComponentParameter(2, nameof(ComboboxInput.Render), s_textarea);
            builder.CloseComponent();
        }

        return test.Context.Render<ComboboxHarness>(p => p
            .Add(x => x.RootParameters, new Dictionary<string, object?> { ["Items"] = s_items })
            .Add(x => x.WithInput, false)
            .Add(x => x.Extra, (RenderFragment)Extra));
    }
}
