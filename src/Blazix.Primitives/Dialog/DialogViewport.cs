using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// DLG §3.5 state object (dialog/viewport/DialogViewport.tsx:35-40). TransitionStatus is mirrored (ADR 04 §2).
/// <summary>Represents the state of a dialog's viewport, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Open">A value indicating whether the dialog is open; projected as <c>data-open</c> or <c>data-closed</c>.</param>
/// <param name="TransitionStatus">
/// The phase of the dialog's enter or exit transition, or <see langword="null"/> while none is in progress; its
/// <c>data-starting-style</c> and <c>data-ending-style</c> attributes are written by the browser-side behavior.
/// </param>
/// <param name="Nested">A value indicating whether the dialog is nested in another dialog; projected as <c>data-nested</c>.</param>
/// <param name="NestedDialogOpen">A value indicating whether a dialog nested in this one is open; projected as <c>data-nested-dialog-open</c>.</param>
[PartState]
public sealed partial record DialogViewportState(bool Open, [property: MirroredField] TransitionStatus? TransitionStatus, bool Nested, bool NestedDialogOpen);

// dialog/viewport/DialogViewportDataAttributes.ts (DLG-DOM-14/15).
/// <summary>Provides the names of the data attributes a dialog's viewport renders.</summary>
public static class DialogViewportDataAttributes
{
    /// <summary>Represents the attribute present while the dialog is open: <c>data-open</c>.</summary>
    public const string Open = "data-open";

    /// <summary>Represents the attribute present while the dialog is closed: <c>data-closed</c>.</summary>
    public const string Closed = "data-closed";

    /// <summary>Represents the attribute present while the dialog begins animating in: <c>data-starting-style</c>.</summary>
    public const string StartingStyle = "data-starting-style";

    /// <summary>Represents the attribute present while the dialog is animating out: <c>data-ending-style</c>.</summary>
    public const string EndingStyle = "data-ending-style";

    /// <summary>Represents the attribute present while the dialog is nested in another dialog: <c>data-nested</c>.</summary>
    public const string Nested = "data-nested";

    /// <summary>Represents the attribute present while a dialog nested in this one is open: <c>data-nested-dialog-open</c>.</summary>
    public const string NestedDialogOpen = "data-nested-dialog-open";
}

// Base UI's Dialog.Viewport (dialog/viewport/DialogViewport.tsx); the contract is docs/components/dialog.md (§3.5, §5.4). A
// thin part over DialogViewportBehavior, which AlertDialogViewport shares (ADR 08 Amendments 2026-10-27).
/// <summary>
/// Represents a positioning container for the dialog's popup that can be made scrollable, Base UI's
/// <c>Dialog.Viewport</c>, rendered as a <c>div</c>.
/// </summary>
/// <remarks>Place it inside the dialog's portal, around the popup.</remarks>
public sealed class DialogViewport : PartBase<DialogViewportState>
{
    private static readonly StateDataAttribute[] s_nestedDialogOpen = [new(DialogViewportDataAttributes.NestedDialogOpen, string.Empty)];

    // dialog/utils/stateAttributesMapping.ts: the popup's open mapping, and nestedDialogOpen as data-nested-dialog-open
    // (Nested keeps the default projection, data-nested).
    private static readonly StateAttributesMapping<DialogViewportState> s_mapping = PopupStateMapping.CreateOpen<DialogViewportState>()
        .Map<bool>(nameof(DialogViewportState.NestedDialogOpen), static open => open ? s_nestedDialogOpen : null);

    private DialogViewportBehavior? behavior;
    private DialogViewportState state = new(false, null, false, false);

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private IDialogRootContext? Context { get; set; }

    [CascadingParameter]
    private PopupPortalContext? Portal { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override DialogViewportState State
    {
        get
        {
            IDialogRootContext root = behavior!.Context;
            bool nestedOpen = root.NestedOpenDialogCount > 0;
            if (state.Open != root.Open || state.TransitionStatus != root.TransitionStatus || state.Nested != root.Nested || state.NestedDialogOpen != nestedOpen)
            {
                state = new DialogViewportState(root.Open, root.TransitionStatus, root.Nested, nestedOpen);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<DialogViewportState>? StateAttributesMapping => s_mapping;

    // For the fixtures: completes once the transitions handle is attached.
    internal Task TwinAttached => behavior?.TwinAttached ?? Task.CompletedTask;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (Context is null)
        {
            throw DialogParts.MissingRoot("Dialog");
        }

        behavior = new DialogViewportBehavior("Dialog", JSRuntime, StateHasChanged, InvokeAsync);
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
