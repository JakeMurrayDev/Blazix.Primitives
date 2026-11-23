using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace Iodine.Headless.Fixtures.Client.ToastFixtures;

/// <summary>
/// The base of the Toast fixtures (issue 280, Deliverable 12: readiness markers, no sleeps). It reads the page's query
/// (<c>?name=value&amp;…</c>), records each toast's close and removal (<c>data-closed</c>, <c>data-removed</c>: the toast
/// ids in order) and every report of the <c>toast</c> module the parts applied (<c>data-reports</c>: the kinds in order),
/// renders <c>data-attached</c> once the tracked viewport's handle has attached, and counts the sync button the specs
/// click as a barrier. The provider is tracked by assigning it with <c>@ref</c> to <see cref="Provider"/>, the viewport to
/// <see cref="Viewport"/>.
/// </summary>
public abstract class ToastFixtureBase : ComponentBase, IDisposable
{
    private readonly List<string> closed = [];
    private readonly List<string> removed = [];
    private readonly List<string> reports = [];
    private Dictionary<string, string>? query;
    private ToastProvider<NoPayload>? provider;
    private ToastViewport<NoPayload>? viewport;
    private Action<string>? onReport;
    private int added;

    protected bool Attached { get; private set; }

    protected int Sync { get; set; }

    protected string Closed => string.Join(' ', closed);

    protected string Removed => string.Join(' ', removed);

    protected string Reports => string.Join(' ', reports);

    [Inject]
    protected ToastManager Manager { get; set; } = null!;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    /// <summary>Sets the provider whose store's reports are recorded.</summary>
    protected ToastProvider<NoPayload>? Provider
    {
        get => provider;
        set => provider ??= value;
    }

    /// <summary>Sets the viewport whose handle the page waits for.</summary>
    protected ToastViewport<NoPayload>? Viewport
    {
        get => viewport;
        set => viewport ??= value;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (provider is not null && onReport is not null)
        {
            provider.Store.ReportApplied -= onReport;
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

    /// <summary>Reads an integer query parameter.</summary>
    /// <param name="name">The parameter's name.</param>
    /// <returns>The value, or <see langword="null"/> without the parameter.</returns>
    protected int? QueryInt(string name) =>
        Query(name, string.Empty) is { Length: > 0 } text ? int.Parse(text, CultureInfo.InvariantCulture) : null;

    /// <summary>Returns a toast that records its close and removal, with an id the page numbers unless one is given.</summary>
    /// <param name="id">The id, or <see langword="null"/> for the next numbered one.</param>
    /// <returns>The toast to add.</returns>
    protected ToastObject Recorded(string? id = null)
    {
        string resolved = id ?? $"t{++added}";
        return new ToastObject
        {
            Id = resolved,
            OnClose = () => Record(closed, resolved),
            OnRemove = () => Record(removed, resolved),
        };
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || !RendererInfo.IsInteractive)
        {
            return;
        }

        if (provider is not null)
        {
            onReport = kind =>
            {
                reports.Add(kind);
                StateHasChanged();
            };
            provider.Store.ReportApplied += onReport;
        }

        if (viewport is not null)
        {
            await viewport.TwinAttached;
        }

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

    private void Record(List<string> list, string id)
    {
        list.Add(id);
        StateHasChanged();
    }
}
