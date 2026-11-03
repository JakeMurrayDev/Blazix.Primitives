using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// UTL-DOM-32..35 (shared-utilities.md §4.12), internals/csp-context/CSPContext.tsx. Cascaded fixed by CspProvider (ADR 16
// rule 9): a change reaches the readers through the listeners below, never through the cascade. Without a provider the
// configuration is Base UI's default, { disableStyleElements: false, nonce: undefined } (UTL-DOM-35). Readers: the scrollbar
// style emitter's callers (Select's popup in M5 track 9; ScrollArea in M6).
/// <summary>Holds the content security policy configuration a <see cref="CspProvider"/> gives its descendants.</summary>
internal sealed class CspProviderContext
{
    private readonly List<Action> listeners = [];

    /// <summary>Gets the nonce placed on the inline style and script elements the parts render.</summary>
    /// <value>The nonce, or <see langword="null"/> for none.</value>
    public string? Nonce { get; private set; }

    /// <summary>Gets a value indicating whether the parts render no inline style elements.</summary>
    public bool DisableStyleElements { get; private set; }

    /// <summary>Reads the nonce of the nearest provider.</summary>
    /// <param name="context">The cascaded context, or <see langword="null"/> outside a provider.</param>
    /// <returns>The nonce, or <see langword="null"/>.</returns>
    public static string? ReadNonce(CspProviderContext? context) => context?.Nonce;

    /// <summary>Reads whether the nearest provider disables the inline style elements.</summary>
    /// <param name="context">The cascaded context, or <see langword="null"/> outside a provider.</param>
    /// <returns><see langword="true"/> when the nearest provider disables them; <see langword="false"/> outside a provider.</returns>
    public static bool ReadDisableStyleElements(CspProviderContext? context) => context?.DisableStyleElements ?? false;

    /// <summary>Applies the provider's values and notifies the readers when either changed.</summary>
    /// <param name="nonce">The nonce, or <see langword="null"/>.</param>
    /// <param name="disableStyleElements">Whether inline style elements are disabled.</param>
    public void Set(string? nonce, bool disableStyleElements)
    {
        if (string.Equals(nonce, Nonce, StringComparison.Ordinal) && disableStyleElements == DisableStyleElements)
        {
            return;
        }

        Nonce = nonce;
        DisableStyleElements = disableStyleElements;

        // A listener may remove itself or another: iterating backwards with a bounds check never skips one still there.
        for (int i = listeners.Count - 1; i >= 0; i--)
        {
            if (i < listeners.Count)
            {
                listeners[i]();
            }
        }
    }

    /// <summary>Adds a reader's action, run after the values changed.</summary>
    /// <param name="listener">The action.</param>
    public void AddListener(Action listener) => listeners.Add(listener);

    /// <summary>Removes a reader's action.</summary>
    /// <param name="listener">The action.</param>
    public void RemoveListener(Action listener) => listeners.Remove(listener);

    internal RenderFragment Cascade(RenderFragment? content) => builder =>
    {
        builder.OpenComponent<CascadingValue<CspProviderContext>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<CspProviderContext>.Value), this);
        builder.AddComponentParameter(2, nameof(CascadingValue<CspProviderContext>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<CspProviderContext>.ChildContent), content);
        builder.CloseComponent();
    };
}
