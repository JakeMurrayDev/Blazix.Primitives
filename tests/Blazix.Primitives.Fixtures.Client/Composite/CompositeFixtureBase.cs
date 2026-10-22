using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Blazix.Primitives.Fixtures.Client.CompositeFixtures;

/// <summary>
/// The base of the <c>composite-*</c> fixtures (tests/browser/composite.spec.ts): renders <c>data-attached</c> once every
/// root of the page has its browser-side behavior attached, and counts the sync button the specs click as a barrier.
/// </summary>
public abstract class CompositeFixtureBase : ComponentBase
{
    protected bool Attached { get; private set; }

    protected int Sync { get; set; }

    /// <summary>The roots of the page, read after the first interactive render.</summary>
    protected abstract IEnumerable<FixtureCompositeRoot?> Roots { get; }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || !RendererInfo.IsInteractive)
        {
            return;
        }

        await Task.WhenAll(Roots.Select(root => root?.TwinAttached ?? Task.CompletedTask));
        Attached = true;
        StateHasChanged();
    }
}

/// <summary>What the consumer's C# key handlers of one case saw: the counts the propagation probe and the veto read.</summary>
public sealed class CompositeKeyProbe
{
    public int RootKeys { get; private set; }

    public int ItemKeys { get; private set; }

    public int OuterKeys { get; private set; }

    public string LastRootKey { get; private set; } = "";

    /// <summary>Whether the root's consumer handler calls <c>PreventPartHandler()</c>.</summary>
    public bool Veto { get; set; }

    public void OnRootKey(KeyboardEventArgs e)
    {
        RootKeys++;
        LastRootKey = e.Key;
        if (Veto)
        {
            e.PreventPartHandler();
        }
    }

    public void OnItemKey(KeyboardEventArgs e) => ItemKeys++;

    public void OnOuterKey(KeyboardEventArgs e) => OuterKeys++;
}
