using Iodine.Headless.Rendering;
using Microsoft.AspNetCore.Components;

namespace Iodine.Headless;

// DRW §3.2 (drawer/indent-background/DrawerIndentBackground.tsx:32-34): the indent background's state object.
/// <summary>Represents the state of a drawer indent background, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Active">A value indicating whether a drawer under the nearest provider is open; projected as <c>data-active</c> or <c>data-inactive</c>.</param>
[PartState]
public sealed partial record DrawerIndentBackgroundState(bool Active);

// Base UI's Drawer.IndentBackground (drawer/indent-background/DrawerIndentBackground.tsx); the contract is
// docs/components/drawer.md (§3.2; DRW-STR-08, DOM-31, API-13). State attributes only; it works without a provider, then
// permanently inactive.
/// <summary>
/// Represents an element placed behind <see cref="DrawerIndent"/>, Base UI's <c>Drawer.IndentBackground</c>, rendered as a
/// <c>div</c>. It is active while a drawer under the nearest <see cref="DrawerProvider"/> is open.
/// </summary>
public sealed class DrawerIndentBackground : PartBase<DrawerIndentBackgroundState>
{
    private Action? changed;
    private DrawerIndentBackgroundState state = new(false);

    [CascadingParameter]
    private DrawerProviderContext? Provider { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override DrawerIndentBackgroundState State
    {
        get
        {
            bool active = DrawerProviderContext.Read(Provider);
            if (state.Active != active)
            {
                state = new DrawerIndentBackgroundState(active);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<DrawerIndentBackgroundState>? StateAttributesMapping => DrawerIndentMapping<DrawerIndentBackgroundState>.Instance;

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && changed is not null)
        {
            Provider?.RemoveListener(changed);
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void SubscribeSelectors()
    {
        if (Provider is not null)
        {
            changed = StateHasChanged;
            Provider.AddListener(changed);
        }
    }
}
