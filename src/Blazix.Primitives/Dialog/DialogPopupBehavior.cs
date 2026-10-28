using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// The popup every dialog family composes (dialog/popup/DialogPopup.tsx; ADR 08 Amendments 2026-10-27). It configures the
// modules and re-implements none (DLG §11):
// - the focus manager (FocusManagerBehavior, track 4): modal = modal !== false, closeOnFocusOut = !disablePointerDismissal,
//   disabled = !mounted, restoreFocus = popup, the popup as the floating element, the active trigger as the reference,
//   the open type; its options are pushed from the commit path, before the render (ADR 01 Amendments 2026-10-27, design point
//   1 (d) of track 4, chosen by the user 2026-10-27), so the subscription's callback sets them before it renders. The inside
//   guards render through BuildBeforeElement/BuildAfterElement (ADR 02 Amendments 2026-10-27); the module writes their
//   VoiceOver swap. InitialFocus and FinalFocus resolve per open and per close (design point 5 of track 8, settled by the orchestrator 2026-10-28; ADR 08 Amendments 2026-10-28): the
//   resolver wins over the value with a development warning; the default initial target is the popup itself for a touch
//   open (createDefaultInitialFocus, popupStoreUtils.ts:37-40), named by its rendered id so it resolves before the render.
// - the `interactions` floating handle (track 3): the inside-press marks and the floating keydown, after the portal moved.
// - the page scroll lock (track 1's PopupScrollLockBehavior): enabled while open and modal === true, on the popup's document
//   (DLG-DOM-26/27).
// - its `transitions` handle (no `transition: none`, TRN-DOM-05), whose finished enter and exit drive the store.
// - the `popup` module's Popup role (track 5's PopupTwin), after the portal moved: DLG-KEY-09, the arrow keys, Home and End
//   stopped unconditionally (DialogPopup.tsx:82-86; PopupElementOptions.StopCompositeKeys).
// - `--nested-dialogs`, written by C# in the commit path on both modes (DLG-DOM-19, ADR 04 Amendments 2026-10-27).
/// <summary>The behavior of a dialog-family popup.</summary>
internal sealed class DialogPopupBehavior : IAsyncDisposable
{
    private const string OpenSelector = "[data-open]";

    private static readonly InteractionsFloatingOptions s_floating = new();
    private static readonly PopupOptions s_stoppedKeys = new() { Popup = new PopupElementOptions { StopCompositeKeys = true } };

    private readonly string partName;
    private readonly ILogger logger;
    private readonly Action requestRender;
    private readonly Action changed;
    private readonly FocusManagerBehavior focus;
    private readonly InteractionsFloatingBehavior floating;
    private readonly PopupScrollLockBehavior scrollLock;
    private readonly DialogTransitions transitions;
    private readonly PopupModuleAttachment keys;
    private IDialogRootContext? context;
    private PopupPortalContext? portal;
    private IDisposable? subscription;
    private IDisposable? boundaryPart;
    private FocusTargetValue? initialFocus;
    private Func<InteractionType, FocusTargetValue?>? initialFocusResolver;
    private FocusTargetValue? finalFocus;
    private Func<InteractionType, FocusTargetValue?>? finalFocusResolver;
    private string? renderedId;
    private bool wasOpen;
    private FocusTargetValue? resolvedInitial;
    private FocusTargetValue? resolvedFinal;
    private bool elementRegistered;
    private bool warnedInitial;
    private bool warnedFinal;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="DialogPopupBehavior"/> class.</summary>
    /// <param name="partName">The part's name as a consumer writes it, such as <c>DialogPopup</c>, for its warnings.</param>
    /// <param name="jsRuntime">The runtime the twins import their modules through.</param>
    /// <param name="logger">The part's logger, which receives development warnings.</param>
    /// <param name="requestRender">Asks the part to render.</param>
    /// <param name="invokeAsync">Runs work on the part's renderer.</param>
    /// <param name="dispatchException">Routes an exception to the part.</param>
    public DialogPopupBehavior(string partName, IJSRuntime jsRuntime, ILogger logger, Action requestRender, Func<Func<Task>, Task> invokeAsync, Func<Exception, Task> dispatchException)
    {
        this.partName = partName;
        this.logger = logger;
        this.requestRender = requestRender;
        changed = OnChanged;
        focus = new FocusManagerBehavior(jsRuntime, invokeAsync, RequestFocusOutCloseAsync);
        floating = new InteractionsFloatingBehavior(jsRuntime, request => context!.RequestOpenAsync(request.Open, request.Details), dispatchException);
        scrollLock = new PopupScrollLockBehavior(jsRuntime);
        transitions = new DialogTransitions(jsRuntime, externalExit: false, invokeAsync, status => context!.MirrorStatus(status), opened => context!.CompleteTransitionAsync(opened));
        keys = new PopupModuleAttachment(jsRuntime);
    }

    /// <summary>Gets the root's context.</summary>
    public IDialogRootContext Context => context!;

    /// <summary>Gets a task that completes once the transitions, focus, floating and popup handles have attached.</summary>
    public Task TwinAttached => Task.WhenAll(transitions.Attached, focus.TwinAttached, floating.TwinAttached, keys.Attached);

    /// <summary>Attaches the handles after the render, sends their changed options and runs the batch boundary.</summary>
    /// <param name="element">The popup's element.</param>
    /// <param name="id">The id the popup rendered.</param>
    /// <returns>A task that completes when the modules have their options.</returns>
    public async Task OnAfterRenderAsync(ElementReference? element, string? id)
    {
        IDialogRootContext root = context!;
        if (element is not null)
        {
            elementRegistered = true;
            root.SetPopupElement(element, renderedId);
        }

        Task moved = PopupPortalContext.WhenMoved(portal);
        Task transitioned = transitions.SyncAsync(element, root, root.PortalKeepMounted, moved);
        Task focused = focus.AfterRenderAsync(element, id);
        Task pressed = floating.AfterRenderAsync(enabled: true, element, id);
        Task locked = scrollLock.ApplyAsync(element, root.Open && root.Modality == DialogModality.Modal);
        Task stopped = StopCompositeKeysAsync(element, moved);
        await Task.WhenAll(transitioned, focused, pressed, locked, stopped);
        await root.AtBatchBoundaryAsync();
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
        boundaryPart?.Dispose();
        if (elementRegistered && context is not null)
        {
            context.ClearPopupElement();
        }

        // The Popup role's attach can still be in flight behind the module's import (issue 134's class): its attachment is
        // disposed with the part, before the other handles' round trips, so an attach the close overtook finds no element
        // and is dropped instead of failing a twin those round trips would keep live.
        ValueTask keysDisposed = keys.DisposeAsync();
        await focus.DisposeAsync();
        await floating.DisposeAsync();
        await scrollLock.DisposeAsync();
        await transitions.DisposeAsync();
        await keysDisposed;
    }

    /// <summary>Checks the part's placement and subscribes to the root's state, once.</summary>
    /// <param name="rootContext">The enclosing root's context.</param>
    /// <param name="portalContext">The context of the enclosing portal.</param>
    /// <exception cref="InvalidOperationException">The part is not inside its root's portal.</exception>
    public void Subscribe(IDialogRootContext rootContext, PopupPortalContext? portalContext)
    {
        if (portalContext is null || !ReferenceEquals(portalContext, rootContext.Portal))
        {
            throw DialogParts.MissingPortal(rootContext.ComponentName);
        }

        context = rootContext;
        portal = portalContext;
        floating.PortalContext = portalContext;
        floating.SessionToken = rootContext.SessionToken;
        floating.Options = s_floating;
        subscription = rootContext.Subscribe(
            DialogFields.Open | DialogFields.Mounted | DialogFields.TransitionStatus | DialogFields.UnmountRequests | DialogFields.Nested
                | DialogFields.NestedOpenDialogCount | DialogFields.Modality | DialogFields.DisablePointerDismissal | DialogFields.Role
                | DialogFields.Labels | DialogFields.PopupId | DialogFields.ActiveTriggerId | DialogFields.OpenMethod | DialogFields.OpenChange,
            changed);
        boundaryPart = rootContext.RegisterBoundaryPart();
    }

    /// <summary>Applies the part's focus parameters and its rendered id, and sends the focus manager's inputs.</summary>
    /// <param name="initial">The part's <c>InitialFocus</c>.</param>
    /// <param name="initialResolver">The part's <c>InitialFocusResolver</c>.</param>
    /// <param name="final">The part's <c>FinalFocus</c>.</param>
    /// <param name="finalResolver">The part's <c>FinalFocusResolver</c>.</param>
    /// <param name="id">The id the popup renders.</param>
    public void Apply(FocusTargetValue? initial, Func<InteractionType, FocusTargetValue?>? initialResolver, FocusTargetValue? final, Func<InteractionType, FocusTargetValue?>? finalResolver, string? id)
    {
        initialFocus = initial;
        initialFocusResolver = initialResolver;
        finalFocus = final;
        finalFocusResolver = finalResolver;
        renderedId = id;
        if (DevelopmentWarnings.IsEnabled)
        {
            WarnBoth(initial is not null && initialResolver is not null, "InitialFocus", ref warnedInitial);
            WarnBoth(final is not null && finalResolver is not null, "FinalFocus", ref warnedFinal);
        }

        PushFocusInputs();
    }

    /// <summary>Adds the popup's attributes.</summary>
    /// <param name="attributes">The part's attributes.</param>
    /// <param name="hasConsumerId">Whether the consumer supplied an <c>id</c>.</param>
    public void AddAttributes(AttributeList attributes, bool hasConsumerId)
    {
        IDialogRootContext root = context!;
        if (!hasConsumerId && root.FloatingId is { } floatingId)
        {
            attributes.Set("id", floatingId);
        }

        if (root.TitleElementId is { } title)
        {
            attributes.Set("aria-labelledby", title);
        }

        if (root.DescriptionElementId is { } description)
        {
            attributes.Set("aria-describedby", description);
        }

        attributes.Set("role", root.Role);
        attributes.Set("tabindex", "-1");
        attributes.Set(PopupMarkerAttributes.Focusable, string.Empty);
        if (!root.Mounted)
        {
            attributes.Set("hidden", true);
        }

        attributes.Set("style", NestedDialogsStyle(root.NestedOpenDialogCount));
        transitions.AddStatusAttributes(attributes, root.TransitionStatus);
    }

    /// <summary>Renders the inside guard before the popup.</summary>
    /// <param name="builder">The part's render tree builder.</param>
    public void BuildBeforeElement(RenderTreeBuilder builder) => focus.BuildBeforeElement(builder);

    /// <summary>Renders the inside guard after the popup.</summary>
    /// <param name="builder">The part's render tree builder.</param>
    public void BuildAfterElement(RenderTreeBuilder builder) => focus.BuildAfterElement(builder);

    // DLG-DOM-19: the count as a number; the common values are cached so a render allocates nothing.
    private static string NestedDialogsStyle(int count) => count switch
    {
        0 => DialogPopupCssVars.NestedDialogs + ": 0",
        1 => DialogPopupCssVars.NestedDialogs + ": 1",
        2 => DialogPopupCssVars.NestedDialogs + ": 2",
        _ => DialogPopupCssVars.NestedDialogs + ": " + count.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };

    // The commit path: the focus manager's inputs first, then the render that applies them.
    private void OnChanged()
    {
        PushFocusInputs();
        requestRender();
    }

    private Task RequestFocusOutCloseAsync(EventSnapshot snapshot) =>
        context!.RequestOpenAsync(false, new OpenChangeEventDetails(EventReason.FocusOut, snapshot));

    private void PushFocusInputs()
    {
        if (disposed || context is null)
        {
            return;
        }

        IDialogRootContext root = context;
        bool open = root.Open;

        // A resolver runs once per open (with the open type; Base UI passes `type || ''`) and once per close (with the type
        // the focus manager classifies); a value follows the parameter.
        if (open && (!wasOpen || initialFocusResolver is null))
        {
            resolvedInitial = ResolveInitial(root.OpenMethod);
        }

        if (finalFocusResolver is null)
        {
            resolvedFinal = finalFocus;
        }
        else if (!open && wasOpen)
        {
            resolvedFinal = finalFocusResolver(CloseTypeClassifier.Classify(root.LastCloseEvent, focus.RecordedInteractionType)) ?? new FocusTargetValue(false);
        }

        wasOpen = open;
        focus.SetInputs(new FocusManagerInputs
        {
            Open = open,
            Modal = root.Modality != DialogModality.NonModal,
            Disabled = !root.Mounted,
            InitialFocus = resolvedInitial,
            ReturnFocus = resolvedFinal,
            CloseOnFocusOut = !root.DisablePointerDismissal,
            OpenInteractionType = root.OpenMethod,
            CloseEvent = root.LastCloseEvent,
            OpenSelector = OpenSelector,
            TreatPopupAsFloatingElement = true,
            DomReferenceId = root.ActiveTriggerId,
            RestoreFocus = FocusManagerTwin.RestorePopup,
            Relay = root.OpenChange,
            SessionToken = root.SessionToken,
        });

        // After the inputs: set before them, the portal would receive the default (enabled) state and render once for it.
        focus.Portal = portal;
    }

    private FocusTargetValue? ResolveInitial(InteractionType? openMethod)
    {
        if (initialFocusResolver is { } resolver)
        {
            // A null result is Base UI's undefined: focus is not moved.
            return resolver(openMethod ?? InteractionType.Unknown) ?? new FocusTargetValue(false);
        }

        if (initialFocus is not null)
        {
            return initialFocus;
        }

        // createDefaultInitialFocus: the popup itself for a touch open, else the default target.
        return openMethod == InteractionType.Touch && renderedId is { } id ? new FocusTargetValue(id) : default(FocusTargetValue?);
    }

    private void WarnBoth(bool both, string parameter, ref bool warned)
    {
        if (!both)
        {
            warned = false;
            return;
        }

        if (!warned)
        {
            warned = true;
            DevelopmentWarnings.Write(logger, $"Blazix: `{partName}` received both `{parameter}` and `{parameter}Resolver`. `{parameter}Resolver` is used; set only one of them.");
        }
    }

    // A portaled part attaches once the portal has moved its node (design point 3 of M4 track 1).
    private async Task StopCompositeKeysAsync(ElementReference? element, Task moved)
    {
        if (element is not { } target)
        {
            return;
        }

        if (!moved.IsCompleted)
        {
            await moved;
        }

        await keys.SyncAsync(target, s_stoppedKeys);
    }
}
