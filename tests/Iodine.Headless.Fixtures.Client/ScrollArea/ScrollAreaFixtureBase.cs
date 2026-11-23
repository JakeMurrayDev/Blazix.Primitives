using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Fixtures.Client.ScrollAreaFixtures;

/// <summary>
/// The base of the ScrollArea fixtures (Deliverable 4 of the ScrollArea brief: readiness markers, no sleeps). Renders
/// <c>data-attached</c> once every tracked part rendered at the first interactive render has its <c>scroll-area</c> handle
/// attached, <c>data-measured</c> once the tracked root's first measurement reached C#, and <c>data-reports</c>, the number of
/// changes of the root's shared state C# has applied; counts the sync button the specs click as a barrier; and reads the
/// page's query (<c>?name=value&amp;…</c>). A part is tracked by assigning it with <c>@ref</c> to one of the setters below.
/// </summary>
public abstract class ScrollAreaFixtureBase : ComponentBase, IDisposable
{
    private readonly List<Func<Task>> attached = [];
    private Dictionary<string, string>? query;
    private ScrollAreaRoot? root;
    private Action? listener;

    protected bool Attached { get; private set; }

    protected bool Measured { get; private set; }

    protected int Reports { get; private set; }

    protected int Sync { get; set; }

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    /// <summary>Sets the root to track: its handle's attach and its shared state.</summary>
    protected ScrollAreaRoot? Root
    {
        get => root;
        set
        {
            Track(value, part => part.TwinAttached);
            root ??= value;
        }
    }

    /// <summary>Sets a viewport to track.</summary>
    protected ScrollAreaViewport? Viewport
    {
        set => Track(value, part => part.TwinAttached);
    }

    /// <summary>Sets a content to track.</summary>
    protected ScrollAreaContent? Content
    {
        set => Track(value, part => part.TwinAttached);
    }

    /// <summary>Sets a scrollbar rendered at the first interactive render to track.</summary>
    protected ScrollAreaScrollbar? Scrollbar
    {
        set => Track(value, part => part.TwinAttached);
    }

    /// <summary>Sets a thumb rendered at the first interactive render to track.</summary>
    protected ScrollAreaThumb? Thumb
    {
        set => Track(value, part => part.TwinAttached);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (root is not null && listener is not null)
        {
            root.Context.RemoveListener(listener);
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>Reads a query parameter of the page.</summary>
    /// <param name="name">The parameter's name.</param>
    /// <param name="fallback">The value without the parameter.</param>
    /// <returns>The parameter's unescaped value, or <paramref name="fallback"/>.</returns>
    protected string Query(string name, string fallback)
    {
        query ??= ParseQuery(new Uri(Navigation.Uri).Query);
        return query.TryGetValue(name, out string? value) ? value : fallback;
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || !RendererInfo.IsInteractive)
        {
            return;
        }

        if (root is not null)
        {
            listener = () =>
            {
                Reports++;
                Measured = root.Measured;
                StateHasChanged();
            };
            root.Context.AddListener(listener);
            Measured = root.Measured;
        }

        await Task.WhenAll(attached.Select(task => task()));
        Attached = true;
        StateHasChanged();
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        Dictionary<string, string> values = new(StringComparer.Ordinal);
        foreach (string pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int equals = pair.IndexOf('=', StringComparison.Ordinal);
            string key = Uri.UnescapeDataString((equals < 0 ? pair : pair[..equals]).Replace('+', ' '));
            values[key] = equals < 0 ? "" : Uri.UnescapeDataString(pair[(equals + 1)..].Replace('+', ' '));
        }

        return values;
    }

    private void Track<T>(T? part, Func<T, Task> twinAttached)
        where T : class
    {
        if (part is not null && !Attached)
        {
            attached.Add(() => twinAttached(part));
        }
    }
}
