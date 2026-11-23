using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// ADL §3.3: Dialog.Backdrop's state object (DLG §3.4, dialog/backdrop/DialogBackdrop.tsx:28-31) under its AlertDialog name.
// TransitionStatus is mirrored (ADR 04 §2).
/// <summary>Represents the state of an alert dialog's backdrop, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Open">A value indicating whether the alert dialog is open; projected as <c>data-open</c> or <c>data-closed</c>.</param>
/// <param name="TransitionStatus">
/// The phase of the alert dialog's enter or exit transition, or <see langword="null"/> while none is in progress; its
/// <c>data-starting-style</c> and <c>data-ending-style</c> attributes are written by the browser-side behavior.
/// </param>
[PartState]
public sealed partial record AlertDialogBackdropState(bool Open, [property: MirroredField] TransitionStatus? TransitionStatus);

// ADL-STR-01: Base UI re-exports DialogBackdropDataAttributes as AlertDialogBackdropDataAttributes (DLG-DOM-09/10).
/// <summary>Provides the names of the data attributes an alert dialog's backdrop renders.</summary>
public static class AlertDialogBackdropDataAttributes
{
    /// <summary>Represents the attribute present while the alert dialog is open: <c>data-open</c>.</summary>
    public const string Open = "data-open";

    /// <summary>Represents the attribute present while the alert dialog is closed: <c>data-closed</c>.</summary>
    public const string Closed = "data-closed";

    /// <summary>Represents the attribute present while the alert dialog begins animating in: <c>data-starting-style</c>.</summary>
    public const string StartingStyle = "data-starting-style";

    /// <summary>Represents the attribute present while the alert dialog is animating out: <c>data-ending-style</c>.</summary>
    public const string EndingStyle = "data-ending-style";
}

// Base UI's AlertDialog.Backdrop, the Dialog.Backdrop component itself (alert-dialog/index.parts.ts:3); the contract is
// docs/components/alert-dialog.md (§3.3, ADL-STR-02 over dialog.md §3.4, §5.3). A sealed part over DialogBackdropBehavior,
// which DialogBackdrop shares (ADR 08 Amendments 2026-10-27).
/// <summary>
/// Represents an overlay displayed beneath the alert dialog's popup, Base UI's <c>AlertDialog.Backdrop</c>, rendered as a
/// <c>div</c>.
/// </summary>
/// <remarks>
/// Pressing it does not close the alert dialog. A backdrop inside a nested alert dialog renders nothing unless
/// <see cref="ForceRender"/> is set.
/// </remarks>
public sealed class AlertDialogBackdrop : PartBase<AlertDialogBackdropState>
{
    private static readonly StateAttributesMapping<AlertDialogBackdropState> s_mapping = PopupStateMapping.CreateOpen<AlertDialogBackdropState>();

    private DialogBackdropBehavior? behavior;
    private AlertDialogBackdropState state = new(false, null);

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private IDialogRootContext? Context { get; set; }

    [CascadingParameter]
    private PopupPortalContext? Portal { get; set; }

    // DLG-API-19
    /// <summary>Gets or sets a value indicating whether the backdrop renders even when the alert dialog is nested in another dialog.</summary>
    /// <value>The default is <see langword="false"/>.</value>
    [Parameter]
    public bool ForceRender { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override AlertDialogBackdropState State
    {
        get
        {
            bool open = behavior!.Open;
            TransitionStatus? status = behavior.TransitionStatus;
            if (state.Open != open || state.TransitionStatus != status)
            {
                state = new AlertDialogBackdropState(open, status);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<AlertDialogBackdropState>? StateAttributesMapping => s_mapping;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    // For the fixtures: completes once the transitions handle is attached.
    internal Task TwinAttached => behavior?.TwinAttached ?? Task.CompletedTask;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (Context is null)
        {
            throw DialogParts.MissingRoot("AlertDialog");
        }

        behavior = new DialogBackdropBehavior(JSRuntime, StateHasChanged, InvokeAsync);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet() => behavior!.Apply(behavior.Renders(ForceRender), Id);

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        Enabled = behavior!.Renders(ForceRender);
        base.BuildRenderTree(builder);
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => behavior!.OnAfterRenderAsync(Enabled, Element, Id, Portal);

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
    protected override void SubscribeSelectors() => behavior!.Subscribe(Context!);

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes) => behavior!.AddAttributes(attributes);
}
