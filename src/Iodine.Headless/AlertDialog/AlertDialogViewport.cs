using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// ADL §3.3: Dialog.Viewport's state object (DLG §3.5, dialog/viewport/DialogViewport.tsx:35-40) under its AlertDialog name.
// TransitionStatus is mirrored (ADR 04 §2).
/// <summary>Represents the state of an alert dialog's viewport, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Open">A value indicating whether the alert dialog is open; projected as <c>data-open</c> or <c>data-closed</c>.</param>
/// <param name="TransitionStatus">
/// The phase of the alert dialog's enter or exit transition, or <see langword="null"/> while none is in progress; its
/// <c>data-starting-style</c> and <c>data-ending-style</c> attributes are written by the browser-side behavior.
/// </param>
/// <param name="Nested">A value indicating whether the alert dialog is nested in another dialog; projected as <c>data-nested</c>.</param>
/// <param name="NestedDialogOpen">A value indicating whether a dialog nested in this one is open; projected as <c>data-nested-dialog-open</c>.</param>
[PartState]
public sealed partial record AlertDialogViewportState(bool Open, [property: MirroredField] TransitionStatus? TransitionStatus, bool Nested, bool NestedDialogOpen);

// ADL-STR-01: Base UI re-exports DialogViewportDataAttributes as AlertDialogViewportDataAttributes (DLG-DOM-14/15).
/// <summary>Provides the names of the data attributes an alert dialog's viewport renders.</summary>
public static class AlertDialogViewportDataAttributes
{
    /// <summary>Represents the attribute present while the alert dialog is open: <c>data-open</c>.</summary>
    public const string Open = "data-open";

    /// <summary>Represents the attribute present while the alert dialog is closed: <c>data-closed</c>.</summary>
    public const string Closed = "data-closed";

    /// <summary>Represents the attribute present while the alert dialog begins animating in: <c>data-starting-style</c>.</summary>
    public const string StartingStyle = "data-starting-style";

    /// <summary>Represents the attribute present while the alert dialog is animating out: <c>data-ending-style</c>.</summary>
    public const string EndingStyle = "data-ending-style";

    /// <summary>Represents the attribute present while the alert dialog is nested in another dialog: <c>data-nested</c>.</summary>
    public const string Nested = "data-nested";

    /// <summary>Represents the attribute present while a dialog nested in this one is open: <c>data-nested-dialog-open</c>.</summary>
    public const string NestedDialogOpen = "data-nested-dialog-open";
}

// Base UI's AlertDialog.Viewport, the Dialog.Viewport component itself (alert-dialog/index.parts.ts:5); the contract is
// docs/components/alert-dialog.md (§3.3, ADL-STR-02 over dialog.md §3.5, §5.4). A sealed part over DialogViewportBehavior,
// which DialogViewport shares (ADR 08 Amendments 2026-10-27).
/// <summary>
/// Represents a positioning container for the alert dialog's popup that can be made scrollable, Base UI's
/// <c>AlertDialog.Viewport</c>, rendered as a <c>div</c>.
/// </summary>
/// <remarks>Place it inside the alert dialog's portal, around the popup.</remarks>
public sealed class AlertDialogViewport : PartBase<AlertDialogViewportState>
{
    private static readonly StateDataAttribute[] s_nestedDialogOpen = [new(AlertDialogViewportDataAttributes.NestedDialogOpen, string.Empty)];

    // dialog/utils/stateAttributesMapping.ts: the popup's open mapping, and nestedDialogOpen as data-nested-dialog-open
    // (Nested keeps the default projection, data-nested).
    private static readonly StateAttributesMapping<AlertDialogViewportState> s_mapping = PopupStateMapping.CreateOpen<AlertDialogViewportState>()
        .Map<bool>(nameof(AlertDialogViewportState.NestedDialogOpen), static open => open ? s_nestedDialogOpen : null);

    private DialogViewportBehavior? behavior;
    private AlertDialogViewportState state = new(false, null, false, false);

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private IDialogRootContext? Context { get; set; }

    [CascadingParameter]
    private PopupPortalContext? Portal { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override AlertDialogViewportState State
    {
        get
        {
            IDialogRootContext root = behavior!.Context;
            bool nestedOpen = root.NestedOpenDialogCount > 0;
            if (state.Open != root.Open || state.TransitionStatus != root.TransitionStatus || state.Nested != root.Nested || state.NestedDialogOpen != nestedOpen)
            {
                state = new AlertDialogViewportState(root.Open, root.TransitionStatus, root.Nested, nestedOpen);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<AlertDialogViewportState>? StateAttributesMapping => s_mapping;

    // For the fixtures: completes once the transitions handle is attached.
    internal Task TwinAttached => behavior?.TwinAttached ?? Task.CompletedTask;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (Context is null)
        {
            throw DialogParts.MissingRoot("AlertDialog");
        }

        behavior = new DialogViewportBehavior("AlertDialog", JSRuntime, StateHasChanged, InvokeAsync);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => behavior!.OnAfterRenderAsync(Enabled, Element, Portal);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && behavior is not null)
        {
            _ = behavior.DisposeAsync().AsTask();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void SubscribeSelectors() => behavior!.Subscribe(Context!, Portal);

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes) => behavior!.AddAttributes(attributes);
}
