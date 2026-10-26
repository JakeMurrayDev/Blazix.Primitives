using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Fixtures.Client.TabsFixtures;

/// <summary>
/// The base of the Tabs fixtures: renders <c>data-attached</c> once every tracked list has its composite and tabs twins
/// attached, every tracked tab its button twin, every tracked panel its transitions twin and every tracked indicator its
/// tabs twin (the specs wait for it before they press a key or click), counts the sync button the specs click as a barrier,
/// and reads the page's query (<c>?name=value&amp;…</c>) for the fixtures whose cases a spec chooses. A part is tracked by
/// assigning it with <c>@ref</c> to one of the setters below, from as many parts as the fixture renders.
/// </summary>
public abstract class TabsFixtureBase : ComponentBase
{
    private readonly List<Func<Task>> attached = [];
    private Dictionary<string, string>? query;

    protected bool Attached { get; private set; }

    protected int Sync { get; set; }

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    /// <summary>Sets a list to track.</summary>
    protected TabsList? List
    {
        set => Track(value, list => list.TwinAttached);
    }

    /// <summary>Sets a tab of type <see cref="int"/> to track.</summary>
    protected TabsTab<int>? IntTab
    {
        set => Track(value, tab => tab.TwinAttached);
    }

    /// <summary>Sets a tab of type <see cref="string"/> to track.</summary>
    protected TabsTab<string>? StringTab
    {
        set => Track(value, tab => tab.TwinAttached);
    }

    /// <summary>Sets a panel of type <see cref="int"/> rendered at the first interactive render to track.</summary>
    protected TabsPanel<int>? IntPanel
    {
        set => Track(value, panel => panel.TwinAttached);
    }

    /// <summary>Sets a panel of type <see cref="string"/> rendered at the first interactive render to track.</summary>
    protected TabsPanel<string>? StringPanel
    {
        set => Track(value, panel => panel.TwinAttached);
    }

    /// <summary>Sets an indicator rendered at the first interactive render to track.</summary>
    protected TabsIndicator? Indicator
    {
        set => Track(value, indicator => indicator.TwinAttached);
    }

    /// <summary>Gets the page's URL without its fragment, so a fixture's link to a fragment stays on the page.</summary>
    /// <value>An absolute URL; <c>#name</c> appended to it names a fragment of this page, where a relative one would
    /// resolve against the document's base URL.</value>
    protected string PageUrl => Navigation.Uri.Split('#')[0];

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
            string key = Uri.UnescapeDataString(equals < 0 ? pair : pair[..equals]);
            values[key] = equals < 0 ? "" : Uri.UnescapeDataString(pair[(equals + 1)..]);
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
