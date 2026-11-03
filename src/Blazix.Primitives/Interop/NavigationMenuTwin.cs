using Microsoft.JSInterop;

namespace Blazix.Primitives.Interop;

// ADR 04 §1 (the `navigation-menu` component module) and Amendments 2026-10-18 and 2026-11-03 (4) (chosen by the user
// 2026-11-03); design points 2-6 of M5 track 5 (settled by the orchestrator 2026-11-04, ADR 04 Amendments 2026-11-04,
// design point 4 the user's). One twin per part that owns a handle: the Root (the menu's state, focus return), each Trigger
// (the size pass, the activation, the open key, its guards, its blur), the top-level List (the propagation guard), a
// top-level Link (its blur), each Content (its exiting inert), the Positioner (focus in and out, the resize report), the
// Popup (the anchoring styles, the size target) and the Viewport (its guards, its target, its blur). No decision point:
// the open key and the activation are reports (design point 4), Escape is the interactions session's.
/// <summary>Represents the C# half of the <c>navigation-menu</c> module.</summary>
internal sealed class NavigationMenuTwin(IJSRuntime jsRuntime)
    : TwinBase<NavigationMenuTwin, NavigationMenuOptions>(jsRuntime, LibraryModule("navigation-menu"), BlazixJsonContext.Default.NavigationMenuOptions)
{
    /// <summary>The part name of the Root's handle.</summary>
    public const string RootPart = "root";

    /// <summary>The part name of a Trigger's handle.</summary>
    public const string TriggerPart = "trigger";

    /// <summary>The part name of the top-level List's handle.</summary>
    public const string ListPart = "list";

    /// <summary>The part name of a top-level Link's handle.</summary>
    public const string LinkPart = "link";

    /// <summary>The part name of a Content's handle.</summary>
    public const string ContentPart = "content";

    /// <summary>The part name of the Positioner's handle.</summary>
    public const string PositionerPart = "positioner";

    /// <summary>The part name of the Popup's handle.</summary>
    public const string PopupPart = "popup";

    /// <summary>The part name of the Viewport's handle.</summary>
    public const string ViewportPart = "viewport";

    /// <summary>Occurs when a trigger reported an activation.</summary>
    public event Action<NavigationMenuActivation>? Activated;

    /// <summary>Occurs when a trigger, a link or a guard reported a focus-out close.</summary>
    public event Action<NavigationMenuFocusOut>? FocusedOut;

    /// <summary>Occurs when the module changed the viewport's inert flag.</summary>
    public event Action<bool>? ViewportInertChanged;

    /// <summary>Occurs when the window was resized while the menu is open, at most once per animation frame.</summary>
    public event Action? Resized;

    /// <summary>Receives a trigger's activation, synchronously on WebAssembly and as a report on Server.</summary>
    /// <param name="payloadJson">The <see cref="NavigationMenuActivation"/> JSON.</param>
    [JSInvokable]
    public void OnActivate(string payloadJson)
    {
        NavigationMenuActivation activation = Deserialize(payloadJson, BlazixJsonContext.Default.NavigationMenuActivation);
        Activated?.Invoke(activation);
    }

    /// <summary>Receives a focus-out close.</summary>
    /// <param name="payloadJson">The <see cref="NavigationMenuFocusOut"/> JSON.</param>
    [JSInvokable]
    public void OnFocusOut(string payloadJson)
    {
        NavigationMenuFocusOut focusOut = Deserialize(payloadJson, BlazixJsonContext.Default.NavigationMenuFocusOut);
        FocusedOut?.Invoke(focusOut);
    }

    /// <summary>Receives a change of the viewport's inert flag.</summary>
    /// <param name="payloadJson">The <see cref="NavigationMenuViewportInertChange"/> JSON.</param>
    [JSInvokable]
    public void OnViewportInertChange(string payloadJson)
    {
        NavigationMenuViewportInertChange change = Deserialize(payloadJson, BlazixJsonContext.Default.NavigationMenuViewportInertChange);
        ViewportInertChanged?.Invoke(change.Inert);
    }

    /// <summary>Receives a window resize while the menu is open.</summary>
    /// <param name="payloadJson">An empty object; the report carries nothing.</param>
    [JSInvokable]
    public void OnResize(string payloadJson)
    {
        _ = payloadJson;
        Resized?.Invoke();
    }

    /// <inheritdoc />
    protected override DecisionPolicy OnDecide(string point, EventSnapshot snapshot) => DecisionPolicy.Run;
}
