using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// The viewport every dialog family composes (dialog/viewport/DialogViewport.tsx; ADR 08 Amendments 2026-10-27): inside the
// portal (DLG-EDGE-03), rendered while the portal renders (DOM-11), hidden while unmounted (DOM-12), pointer-events: none
// before the consumer's style while closed (DOM-13), role="presentation" (ARIA-09); its `transitions` handle ends its exit
// when the store's unmount moves UnmountRequests (DLG-ANI-03), and hides it when kept mounted (DOM-12).
/// <summary>The behavior of a dialog-family viewport.</summary>
internal sealed class DialogViewportBehavior : IAsyncDisposable
{
    private const string ClosedStyle = "pointer-events: none";

    private readonly DialogTransitions transitions;
    private readonly Action requestRender;
    private IDialogRootContext? context;
    private IDisposable? subscription;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="DialogViewportBehavior"/> class.</summary>
    /// <param name="jsRuntime">The runtime the twin imports its module through.</param>
    /// <param name="requestRender">Asks the part to render.</param>
    /// <param name="invokeAsync">Runs work on the part's renderer.</param>
    public DialogViewportBehavior(IJSRuntime jsRuntime, Action requestRender, Func<Func<Task>, Task> invokeAsync)
    {
        this.requestRender = requestRender;
        transitions = new DialogTransitions(jsRuntime, externalExit: true, invokeAsync);
    }

    /// <summary>Gets the root's context.</summary>
    public IDialogRootContext Context => context!;

    /// <summary>Gets a task that completes once the transitions handle has attached.</summary>
    public Task TwinAttached => transitions.Attached;

    /// <summary>Attaches the transitions handle after the render.</summary>
    /// <param name="enabled">Whether the part rendered its element.</param>
    /// <param name="element">The element.</param>
    /// <param name="portal">The context of the enclosing portal.</param>
    /// <returns>A task that completes when the handle has its options.</returns>
    public Task OnAfterRenderAsync(bool enabled, ElementReference? element, PopupPortalContext? portal) =>
        transitions.SyncAsync(enabled ? element : null, context!, context!.PortalKeepMounted, PopupPortalContext.WhenMoved(portal));

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        subscription?.Dispose();
        await transitions.DisposeAsync();
    }

    /// <summary>Checks the part's placement and subscribes to the root's state, once.</summary>
    /// <param name="rootContext">The enclosing root's context.</param>
    /// <param name="portal">The context of the enclosing portal.</param>
    /// <exception cref="InvalidOperationException">The part is not inside its root's portal.</exception>
    public void Subscribe(IDialogRootContext rootContext, PopupPortalContext? portal)
    {
        if (portal is null || !ReferenceEquals(portal, rootContext.Portal))
        {
            throw DialogParts.MissingPortal(rootContext.ComponentName);
        }

        context = rootContext;
        subscription = rootContext.Subscribe(
            DialogFields.Open | DialogFields.Mounted | DialogFields.TransitionStatus | DialogFields.UnmountRequests | DialogFields.Nested | DialogFields.NestedOpenDialogCount,
            requestRender);
    }

    /// <summary>Adds the viewport's attributes.</summary>
    /// <param name="attributes">The part's attributes.</param>
    public void AddAttributes(AttributeList attributes)
    {
        IDialogRootContext root = context!;
        attributes.Set("role", "presentation");
        if (!root.Mounted)
        {
            attributes.Set("hidden", true);
        }

        if (!root.Open)
        {
            attributes.Set("style", ClosedStyle);
        }

        transitions.AddStatusAttributes(attributes, root.TransitionStatus);
    }
}
