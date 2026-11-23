using Iodine.Headless.Interop;
using Iodine.Headless.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Iodine.Headless;

// DRW §3.3 (drawer/indent/DrawerIndent.tsx:71-73): the indent's state object.
/// <summary>Represents the state of a drawer indent, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Active">A value indicating whether a drawer under the nearest provider is open; projected as <c>data-active</c> or <c>data-inactive</c>.</param>
[PartState]
public sealed partial record DrawerIndentState(bool Active);

// DRW-DOM-31, DRW-DOM-32.
/// <summary>Provides the names of the data attributes a drawer indent renders.</summary>
public static class DrawerIndentDataAttributes
{
    /// <summary>Represents the attribute present while a drawer under the provider is open: <c>data-active</c>.</summary>
    public const string Active = "data-active";

    /// <summary>Represents the attribute present while no drawer under the provider is open: <c>data-inactive</c>.</summary>
    public const string Inactive = "data-inactive";
}

// Base UI's Drawer.Indent (drawer/indent/DrawerIndent.tsx); the contract is docs/components/drawer.md (§3.3; DRW-STR-08,
// DOM-31, DOM-32, API-13). It reads the provider's Active through the fixed cascade's listener and works without a
// provider, then permanently inactive. Its inline --drawer-swipe-progress and --drawer-height are the `drawer` module's
// (8b; the Writer cells C# → JS and JS): C# renders the resting 0, and under a provider the module's `indent` handle writes
// the Provider's visual state over it (M6 track 8b slice 3, 2026-11-21).
/// <summary>
/// Represents a wrapper for the page content behind the drawers, Base UI's <c>Drawer.Indent</c>, rendered as a <c>div</c>.
/// It is active while a drawer under the nearest <see cref="DrawerProvider"/> is open.
/// </summary>
public sealed class DrawerIndent : PartBase<DrawerIndentState>
{
    private const string RestingStyle = "--drawer-swipe-progress: 0";

    private Action? changed;
    private DrawerIndentState state = new(false);
    private DrawerTwin? twin;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private DrawerProviderContext? Provider { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override DrawerIndentState State
    {
        get
        {
            bool active = DrawerProviderContext.Read(Provider);
            if (state.Active != active)
            {
                state = new DrawerIndentState(active);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<DrawerIndentState>? StateAttributesMapping => DrawerIndentMapping<DrawerIndentState>.Instance;

    // DRW-DOM-32: the module shows the Provider's visual state; the token is fixed with the provider's cascade.
    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && Provider is { } provider && Element is { } element)
        {
            twin = new DrawerTwin(JSRuntime);
            await twin.AttachAsync(element, new DrawerOptions { Role = DrawerTwin.IndentRole, Provider = provider.Token });
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && changed is not null)
        {
            Provider?.RemoveListener(changed);
        }

        if (disposing && twin is not null)
        {
            _ = twin.DisposeAsync().AsTask();
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

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes) => attributes.Set("style", RestingStyle);
}

// DRW-DOM-31: data-active or data-inactive, never neither, for both indent parts.
internal static class DrawerIndentMapping<TState>
{
    private static readonly StateDataAttribute[] s_active = [new(DrawerIndentDataAttributes.Active, string.Empty)];
    private static readonly StateDataAttribute[] s_inactive = [new(DrawerIndentDataAttributes.Inactive, string.Empty)];

    /// <summary>Gets the mapping of a state record whose <c>Active</c> field projects as <c>data-active</c> or <c>data-inactive</c>.</summary>
    public static StateAttributesMapping<TState> Instance { get; } =
        new StateAttributesMapping<TState>().Map<bool>("Active", static active => active ? s_active : s_inactive);
}
