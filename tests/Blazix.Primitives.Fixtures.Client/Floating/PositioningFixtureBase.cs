using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Fixtures.Client.FloatingFixtures;

/// <summary>
/// The base of the <c>positioning-*</c> fixtures (tests/browser/positioning.spec.ts and the positioner conformance suite,
/// tests/browser/suites/positioner-conformance.ts): renders the case named by the page's <c>?case=</c> query, and
/// <c>data-attached</c> once the case's positioner has its browser-side behavior attached (at once for a case whose
/// positioner mounts later), and counts the sync button the specs click as a barrier.
/// </summary>
public abstract class PositioningFixtureBase : ComponentBase
{
    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    protected bool Attached { get; private set; }

    protected int Sync { get; set; }

    /// <summary>Gets the case the page renders.</summary>
    protected string Case { get; private set; } = "";

    /// <summary>Gets the positioner the case renders at first render, read after the first interactive render.</summary>
    protected FixturePositioner? Positioner { get; set; }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        string query = new Uri(Navigation.Uri).Query;
        const string key = "case=";
        int start = query.IndexOf(key, StringComparison.Ordinal);
        if (start >= 0)
        {
            start += key.Length;
            int end = query.IndexOf('&', start);
            Case = Uri.UnescapeDataString(end < 0 ? query[start..] : query[start..end]);
        }
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || !RendererInfo.IsInteractive)
        {
            return;
        }

        if (Positioner is { } positioner)
        {
            await positioner.TwinAttached;
        }

        Attached = true;
        StateHasChanged();
    }
}
