using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// MNU §3.7 state object (menu/viewport/MenuViewport.tsx; Popover's popupViewportStateMapping). ActivationDirection and
// Transitioning are the `popup` module's report (Interop/PopupPayloads.cs:8): mirrored.
/// <summary>Represents the state of a menu viewport, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="ActivationDirection">The direction from the previous trigger to the new one, or <see langword="null"/>; projected as <c>data-activation-direction</c>.</param>
/// <param name="Transitioning">A value indicating whether the content is moving between two triggers; projected as <c>data-transitioning</c>.</param>
/// <param name="Instant">Why the next transition is skipped, or <see langword="null"/>; projected as <c>data-instant</c>.</param>
[PartState]
public sealed partial record MenuViewportState(
    [property: MirroredField] string? ActivationDirection,
    [property: MirroredField] bool Transitioning,
    MenuInstantType? Instant);

// menu/viewport/MenuViewportDataAttributes.ts (MNU-DOM-12).
/// <summary>Provides the names of the data attributes a menu viewport renders.</summary>
public static class MenuViewportDataAttributes
{
    /// <summary>Represents the attribute that names the direction from the previous trigger to the new one: <c>data-activation-direction</c>.</summary>
    public const string ActivationDirection = PopupViewportDataAttributes.ActivationDirection;

    /// <summary>Represents the attribute of the container that holds the current content: <c>data-current</c>.</summary>
    public const string Current = PopupViewportDataAttributes.Current;

    /// <summary>Represents the attribute of the container that holds the previous content during a switch: <c>data-previous</c>.</summary>
    public const string Previous = PopupViewportDataAttributes.Previous;

    /// <summary>Represents the attribute present while the content is moving between two triggers: <c>data-transitioning</c>.</summary>
    public const string Transitioning = PopupViewportDataAttributes.Transitioning;

    /// <summary>Represents the attribute that names why the next transition is skipped: <c>data-instant</c>.</summary>
    public const string Instant = "data-instant";
}

// menu/viewport/MenuViewportCssVars.ts (MNU-DOM-12).
/// <summary>Provides the names of the CSS variables a menu viewport sets on its previous container.</summary>
public static class MenuViewportCssVars
{
    /// <summary>Represents the previous content's width: <c>--popup-width</c>.</summary>
    public const string PopupWidth = PopupCssVars.PopupWidth;

    /// <summary>Represents the previous content's height: <c>--popup-height</c>.</summary>
    public const string PopupHeight = PopupCssVars.PopupHeight;
}

// Base UI's Menu.Viewport (menu/viewport/MenuViewport.tsx); the contract is docs/components/menu.md §3.7. A thin shell
// over MenuViewportBehavior (Popover's viewport over the `popup` module).
/// <summary>
/// Holds the menu's content and animates it when the menu moves between triggers, Base UI's <c>Menu.Viewport</c>, rendered
/// as a <c>div</c>.
/// </summary>
public sealed class MenuViewport : PartBase<MenuViewportState>
{
    private MenuViewportBehavior? behavior;
    private MenuViewportState? state;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private MenuRootContext? RootContext { get; set; }

    [CascadingParameter]
    private PositionerContext? PositionerContext { get; set; }

    [CascadingParameter]
    private PopupPortalContext? Portal { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    // The pipeline reads the state before the part's attributes, so the record follows the store and the mirrors here.
    /// <inheritdoc />
    protected override MenuViewportState State
    {
        get
        {
            MenuViewportBehavior current = behavior!;
            if (state is null || state.Instant != current.Instant || state.ActivationDirection != current.ActivationDirection || state.Transitioning != current.Transitioning)
            {
                state = new MenuViewportState(current.ActivationDirection, current.Transitioning, current.Instant);
            }

            return state;
        }
    }

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        // The part replaces ChildContent with its own cascade, so a parameter set without content must not keep it.
        ChildContent = null;
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        behavior = new MenuViewportBehavior("Menu", JSRuntime, StateHasChanged, InvokeAsync);
        behavior.Subscribe(RootContext, PositionerContext, Portal);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet() => ChildContent = behavior!.CascadeContent(ChildContent);

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        behavior!.BeforeRender();
        base.BuildRenderTree(builder);
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => behavior!.OnAfterRenderAsync(Element);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && behavior is not null)
        {
            _ = behavior.DisposeAsync().AsTask();
        }

        base.Dispose(disposing);
    }
}
