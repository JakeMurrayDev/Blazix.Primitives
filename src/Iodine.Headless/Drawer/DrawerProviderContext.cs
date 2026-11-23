using Microsoft.AspNetCore.Components;

namespace Iodine.Headless;

// DRW-STA-14, DRW-API-14 (drawer/provider/DrawerProvider.tsx:17-41): the set of open roots under a provider and whether it
// is non-empty. Cascaded fixed by DrawerProvider (ADR 16 rule 9); a change of Active reaches the indent parts through the
// listeners, never through the cascade. Closed and removed roots are not kept (CHANGELOG v1.9.0). The visual-state store
// (DRW-STA-15) is the `drawer` module's (M6 track 8 design point 1).
/// <summary>The open drawers under a <see cref="DrawerProvider"/>.</summary>
internal sealed class DrawerProviderContext
{
    private static int s_count;
    private readonly HashSet<object> openRoots = new(ReferenceEqualityComparer.Instance);
    private readonly List<Action> listeners = [];

    // M6 track 8b slice 3 (2026-11-21): the `drawer` module keys the visual state (DRW-STA-15) by this token.
    /// <summary>Gets the token the provider's visual state is known by in the browser.</summary>
    public string Token { get; } = "iodine-drawer-provider-" + Interlocked.Increment(ref s_count).ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Gets a value indicating whether any drawer under the provider is open.</summary>
    public bool Active => openRoots.Count > 0;

    // For the tests: the roots the set holds (DRW-STA-14, CHANGELOG v1.9.0).
    internal int Count => openRoots.Count;

    /// <summary>Reads whether a part's provider is active.</summary>
    /// <param name="context">The cascaded context, or <see langword="null"/> without a provider.</param>
    /// <returns><see langword="true"/> when a drawer under the provider is open; <see langword="false"/> without a provider.</returns>
    public static bool Read(DrawerProviderContext? context) => context?.Active ?? false;

    /// <summary>Records whether a root is open; a redundant report changes nothing.</summary>
    /// <param name="root">The root.</param>
    /// <param name="open">Whether the root is effectively open.</param>
    public void Report(object root, bool open)
    {
        bool wasActive = Active;
        if (open)
        {
            openRoots.Add(root);
        }
        else
        {
            openRoots.Remove(root);
        }

        if (wasActive != Active)
        {
            Notify();
        }
    }

    /// <summary>Forgets a root that leaves, as a closed report; an unknown root changes nothing.</summary>
    /// <param name="root">The root.</param>
    public void Remove(object root) => Report(root, open: false);

    /// <summary>Adds a listener that runs after every change of <see cref="Active"/>.</summary>
    /// <param name="listener">The listener.</param>
    public void AddListener(Action listener) => listeners.Add(listener);

    /// <summary>Removes a listener added with <see cref="AddListener"/>.</summary>
    /// <param name="listener">The listener.</param>
    public void RemoveListener(Action listener) => listeners.Remove(listener);

    /// <summary>Gets the fragment that cascades this context to <paramref name="content"/>.</summary>
    /// <param name="content">The content that receives the context.</param>
    /// <returns>The fragment.</returns>
    internal RenderFragment Cascade(RenderFragment? content) => builder =>
    {
        builder.OpenComponent<CascadingValue<DrawerProviderContext>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<DrawerProviderContext>.Value), this);
        builder.AddComponentParameter(2, nameof(CascadingValue<DrawerProviderContext>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<DrawerProviderContext>.ChildContent), content);
        builder.CloseComponent();
    };

    private void Notify()
    {
        // A listener may remove itself or another: iterating backwards with a bounds check never skips one still there.
        for (int i = listeners.Count - 1; i >= 0; i--)
        {
            if (i < listeners.Count)
            {
                listeners[i]();
            }
        }
    }
}
