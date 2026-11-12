using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives;

// Base UI's Autocomplete.Value (autocomplete/value/AutocompleteValue.tsx:11-26; AUT-API-18, STR-04's Value clause): the engine's
// current input text, the displayed text including an active inline completion (AriaCombobox.tsx:1714). Its one `children`
// prop, a node or a function of the text, is ChildContent beside TextContent, on the pattern of ComboboxValue.ValueContent.
// Outside a root it renders "" and throws nothing (Base UI reads an optional context). It reads the store the root cascades
// and subscribes to its text: Autocomplete's own part, so no Combobox behaviour serves it.
/// <summary>Represents the text an autocomplete displays, Base UI's <c>Autocomplete.Value</c>, rendered without an element of its own.</summary>
public sealed class AutocompleteValue : ComponentBase, IDisposable
{
    private SelectorSubscription<ComboboxStoreState>? subscription;

    [CascadingParameter]
    private ComboboxStore? Root { get; set; }

    /// <summary>Gets or sets content rendered in place of the text.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>Gets or sets the template called with the displayed text, an empty string when nothing was typed.</summary>
    [Parameter]
    public RenderFragment<string>? TextContent { get; set; }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (Root is { } root)
        {
            subscription = new SelectorSubscription<ComboboxStoreState>(root, StateHasChanged);
            subscription.Select(static state => state.InputValue);
        }
    }

    // AUT-API-18: the function child, then any other content, then the text itself.
    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        string text = Root?.State.InputValue ?? string.Empty;
        if (TextContent is { } textContent)
        {
            builder.AddContent(0, textContent(text));
        }
        else if (ChildContent is { } childContent)
        {
            builder.AddContent(1, childContent);
        }
        else
        {
            builder.AddContent(2, text);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        subscription?.Dispose();
        subscription = null;
    }
}
