using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// DLG §3.4 state object (dialog/backdrop/DialogBackdrop.tsx:28-31). TransitionStatus is mirrored (ADR 04 §2).
/// <summary>Represents the state of a dialog's backdrop, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Open">A value indicating whether the dialog is open; projected as <c>data-open</c> or <c>data-closed</c>.</param>
/// <param name="TransitionStatus">
/// The phase of the dialog's enter or exit transition, or <see langword="null"/> while none is in progress; its
/// <c>data-starting-style</c> and <c>data-ending-style</c> attributes are written by the browser-side behavior.
/// </param>
[PartState]
public sealed partial record DialogBackdropState(bool Open, [property: MirroredField] TransitionStatus? TransitionStatus);

// dialog/backdrop/DialogBackdropDataAttributes.ts (DLG-DOM-09/10).
/// <summary>Provides the names of the data attributes a dialog's backdrop renders.</summary>
public static class DialogBackdropDataAttributes
{
    /// <summary>Represents the attribute present while the dialog is open: <c>data-open</c>.</summary>
    public const string Open = "data-open";

    /// <summary>Represents the attribute present while the dialog is closed: <c>data-closed</c>.</summary>
    public const string Closed = "data-closed";

    /// <summary>Represents the attribute present while the dialog begins animating in: <c>data-starting-style</c>.</summary>
    public const string StartingStyle = "data-starting-style";

    /// <summary>Represents the attribute present while the dialog is animating out: <c>data-ending-style</c>.</summary>
    public const string EndingStyle = "data-ending-style";
}

// Base UI's Dialog.Backdrop (dialog/backdrop/DialogBackdrop.tsx); the contract is docs/components/dialog.md (§3.4, §5.3). A
// thin part over DialogBackdropBehavior, which AlertDialogBackdrop shares (ADR 08 Amendments 2026-10-27).
/// <summary>
/// Represents an overlay displayed beneath the dialog's popup, Base UI's <c>Dialog.Backdrop</c>, rendered as a <c>div</c>.
/// </summary>
/// <remarks>A backdrop inside a nested dialog renders nothing unless <see cref="ForceRender"/> is set.</remarks>
public sealed class DialogBackdrop : PartBase<DialogBackdropState>
{
    private static readonly StateAttributesMapping<DialogBackdropState> s_mapping = PopupStateMapping.CreateOpen<DialogBackdropState>();

    private DialogBackdropBehavior? behavior;
    private DialogBackdropState state = new(false, null);

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private IDialogRootContext? Context { get; set; }

    [CascadingParameter]
    private PopupPortalContext? Portal { get; set; }

    // DLG-API-19
    /// <summary>Gets or sets a value indicating whether the backdrop renders even when the dialog is nested in another.</summary>
    /// <value>The default is <see langword="false"/>.</value>
    [Parameter]
    public bool ForceRender { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override DialogBackdropState State
    {
        get
        {
            bool open = behavior!.Open;
            TransitionStatus? status = behavior.TransitionStatus;
            if (state.Open != open || state.TransitionStatus != status)
            {
                state = new DialogBackdropState(open, status);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<DialogBackdropState>? StateAttributesMapping => s_mapping;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    // For the fixtures: completes once the transitions handle is attached.
    internal Task TwinAttached => behavior?.TwinAttached ?? Task.CompletedTask;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (Context is null)
        {
            throw DialogParts.MissingRoot("Dialog");
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
