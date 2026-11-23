using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// The backdrop every dialog family composes (dialog/backdrop/DialogBackdrop.tsx; ADR 08 Amendments 2026-10-27):
// - rendered while its root is in the tree unless nested without ForceRender (DLG-STR-05, API-19), hidden while unmounted
//   (DOM-08), user-select: none before the consumer's style (DOM-07), role="presentation" (ARIA-08);
// - its rendered id is the store's backdrop id, which the outside press names (DLG-PTR-05/10; Base UI's backdropRef);
// - its `transitions` handle ends its exit when the store's unmount moves UnmountRequests (DLG-ANI-03, ExternalExit).
/// <summary>The behavior of a dialog-family backdrop.</summary>
internal sealed class DialogBackdropBehavior : IAsyncDisposable
{
    private const string Style = "user-select: none; -webkit-user-select: none";

    private readonly DialogTransitions transitions;
    private readonly Action requestRender;
    private IDialogRootContext? context;
    private IDisposable? subscription;
    private string? registeredId;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="DialogBackdropBehavior"/> class.</summary>
    /// <param name="jsRuntime">The runtime the twin imports its module through.</param>
    /// <param name="requestRender">Asks the part to render.</param>
    /// <param name="invokeAsync">Runs work on the part's renderer.</param>
    public DialogBackdropBehavior(IJSRuntime jsRuntime, Action requestRender, Func<Func<Task>, Task> invokeAsync)
    {
        this.requestRender = requestRender;
        transitions = new DialogTransitions(jsRuntime, externalExit: true, invokeAsync);
    }

    /// <summary>Gets a value indicating whether the dialog is effectively open.</summary>
    public bool Open => context!.Open;

    /// <summary>Gets the derived transition status.</summary>
    public TransitionStatus? TransitionStatus => context!.TransitionStatus;

    /// <summary>Gets a task that completes once the transitions handle has attached.</summary>
    public Task TwinAttached => transitions.Attached;

    /// <summary>Attaches the transitions handle and records the backdrop's id after the render.</summary>
    /// <param name="enabled">Whether the part rendered its element.</param>
    /// <param name="element">The element.</param>
    /// <param name="id">The id the part rendered.</param>
    /// <param name="portal">The context of the portal the backdrop is rendered inside, or <see langword="null"/>.</param>
    /// <returns>A task that completes when the handle has its options.</returns>
    public Task OnAfterRenderAsync(bool enabled, ElementReference? element, string? id, PopupPortalContext? portal)
    {
        Register(enabled ? id : null);
        return transitions.SyncAsync(enabled ? element : null, context!, keepMounted: true, PopupPortalContext.WhenMoved(portal));
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        subscription?.Dispose();
        Register(null);
        await transitions.DisposeAsync();
    }

    /// <summary>Subscribes to the root's state, once.</summary>
    /// <param name="rootContext">The enclosing root's context.</param>
    public void Subscribe(IDialogRootContext rootContext)
    {
        context = rootContext;
        subscription = rootContext.Subscribe(DialogFields.Open | DialogFields.Mounted | DialogFields.TransitionStatus | DialogFields.UnmountRequests | DialogFields.Nested, requestRender);
    }

    /// <summary>Determines whether the backdrop renders.</summary>
    /// <param name="forceRender">The part's <c>ForceRender</c>.</param>
    /// <returns><see langword="false"/> for a nested root's backdrop without <paramref name="forceRender"/>.</returns>
    public bool Renders(bool forceRender) => forceRender || !context!.Nested;

    /// <summary>Records the backdrop's id at the parameter set, so the dismissal options name it before the next press.</summary>
    /// <param name="enabled">Whether the part renders.</param>
    /// <param name="id">The id the part renders.</param>
    public void Apply(bool enabled, string? id) => Register(enabled ? id : null);

    /// <summary>Adds the backdrop's attributes.</summary>
    /// <param name="attributes">The part's attributes.</param>
    public void AddAttributes(AttributeList attributes)
    {
        attributes.Set("role", "presentation");
        if (!context!.Mounted)
        {
            attributes.Set("hidden", true);
        }

        attributes.Set("style", Style);
        transitions.AddStatusAttributes(attributes, context.TransitionStatus);
    }

    private void Register(string? id)
    {
        if (string.Equals(registeredId, id, StringComparison.Ordinal) || context is null)
        {
            return;
        }

        // Only the backdrop that registered last clears the store's id (Base UI's ref is the last attached backdrop).
        if (id is null && !string.Equals(context.BackdropElementId, registeredId, StringComparison.Ordinal))
        {
            registeredId = null;
            return;
        }

        registeredId = id;
        context.SetBackdropElementId(id);
    }
}
