using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// ADL §3.3: Dialog.Popup's state object (DLG §3.6, dialog/popup/DialogPopup.tsx:64-69) under its AlertDialog name.
// TransitionStatus is mirrored (ADR 04 §2).
/// <summary>Represents the state of an alert dialog's popup, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Open">A value indicating whether the alert dialog is open; projected as <c>data-open</c> or <c>data-closed</c>.</param>
/// <param name="TransitionStatus">
/// The phase of the alert dialog's enter or exit transition, or <see langword="null"/> while none is in progress; its
/// <c>data-starting-style</c> and <c>data-ending-style</c> attributes are written by the browser-side behavior.
/// </param>
/// <param name="Nested">A value indicating whether the alert dialog is nested in another dialog; projected as <c>data-nested</c>.</param>
/// <param name="NestedDialogOpen">A value indicating whether a dialog nested in this one is open; projected as <c>data-nested-dialog-open</c>.</param>
[PartState]
public sealed partial record AlertDialogPopupState(bool Open, [property: MirroredField] TransitionStatus? TransitionStatus, bool Nested, bool NestedDialogOpen);

// ADL-STR-01: Base UI re-exports DialogPopupDataAttributes as AlertDialogPopupDataAttributes (DLG-DOM-20..22).
/// <summary>Provides the names of the data attributes an alert dialog's popup renders.</summary>
public static class AlertDialogPopupDataAttributes
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

// ADL-STR-01: Base UI re-exports DialogPopupCssVars as AlertDialogPopupCssVariables (DLG-DOM-19); ADR 08 §3's
// <Part>CssVars name.
/// <summary>Provides the names of the CSS variables an alert dialog's popup sets.</summary>
public static class AlertDialogPopupCssVars
{
    /// <summary>Represents the number of open dialogs nested in this one: <c>--nested-dialogs</c>.</summary>
    public const string NestedDialogs = "--nested-dialogs";
}

// Base UI's AlertDialog.Popup, the Dialog.Popup component itself (alert-dialog/index.parts.ts:6); the contract is
// docs/components/alert-dialog.md (§3.3, §5, §6; ADL-STR-02 over dialog.md §3.6, §5.5, §6). A sealed part over
// DialogPopupBehavior, which DialogPopup shares (ADR 08 Amendments 2026-10-27). The role, the trap and the focus-out rule
// come from the root's mode.
/// <summary>
/// Represents a container for the alert dialog's contents, Base UI's <c>AlertDialog.Popup</c>, rendered as a <c>div</c>
/// with the <c>alertdialog</c> role.
/// </summary>
/// <remarks>
/// Place it inside the alert dialog's portal. Focus moves into it when the alert dialog opens, stays inside it while the
/// alert dialog is open, and returns to the trigger when it closes.
/// </remarks>
public sealed class AlertDialogPopup : PartBase<AlertDialogPopupState>
{
    private static readonly StateDataAttribute[] s_nestedDialogOpen = [new(AlertDialogPopupDataAttributes.NestedDialogOpen, string.Empty)];

    // dialog/utils/stateAttributesMapping.ts: the popup's open mapping, and nestedDialogOpen as data-nested-dialog-open
    // (Nested keeps the default projection, data-nested).
    private static readonly StateAttributesMapping<AlertDialogPopupState> s_mapping = PopupStateMapping.CreateOpen<AlertDialogPopupState>()
        .Map<bool>(nameof(AlertDialogPopupState.NestedDialogOpen), static open => open ? s_nestedDialogOpen : null);

    private DialogPopupBehavior? behavior;
    private AlertDialogPopupState state = new(false, null, false, false);

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<AlertDialogPopup> Logger { get; set; } = null!;

    private bool HasConsumerId => AdditionalAttributes is not null && AdditionalAttributes.ContainsKey("id");

    private string? RenderedId => HasConsumerId ? AdditionalAttributes!["id"] as string : Context!.FloatingId;

    [CascadingParameter]
    private IDialogRootContext? Context { get; set; }

    [CascadingParameter]
    private PopupPortalContext? Portal { get; set; }

    // DLG-API-20
    /// <summary>Gets or sets where focus moves when the alert dialog opens.</summary>
    /// <value>
    /// <see langword="null"/>, the default, for the first tabbable element inside the popup, or the popup itself when the
    /// alert dialog was opened by touch; <see langword="true"/> for the first tabbable element; <see langword="false"/> to leave
    /// focus where it is; an element or the id of an element to focus it.
    /// </value>
    [Parameter]
    public FocusTargetValue? InitialFocus { get; set; }

    /// <summary>Gets or sets a function that decides where focus moves when the alert dialog opens, from how it was opened.</summary>
    /// <value>
    /// A function called once per open with the interaction type (<see cref="InteractionType.Unknown"/> when it is not
    /// known). Its result is used as <see cref="InitialFocus"/> would be, except that <see langword="null"/> leaves focus
    /// where it is. When it is set, <see cref="InitialFocus"/> is ignored.
    /// </value>
    [Parameter]
    public Func<InteractionType, FocusTargetValue?>? InitialFocusResolver { get; set; }

    // DLG-API-21
    /// <summary>Gets or sets where focus moves when the alert dialog closes.</summary>
    /// <value>
    /// <see langword="null"/>, the default, or <see langword="true"/> for the trigger, else the element focused before the
    /// alert dialog opened; <see langword="false"/> to leave focus where it is; an element or the id of an element to focus it.
    /// </value>
    [Parameter]
    public FocusTargetValue? FinalFocus { get; set; }

    /// <summary>Gets or sets a function that decides where focus moves when the alert dialog closes, from how it was closed.</summary>
    /// <value>
    /// A function called once per close with the interaction type. Its result is used as <see cref="FinalFocus"/> would be,
    /// except that <see langword="null"/> leaves focus where it is. When it is set, <see cref="FinalFocus"/> is ignored.
    /// </value>
    [Parameter]
    public Func<InteractionType, FocusTargetValue?>? FinalFocusResolver { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override AlertDialogPopupState State
    {
        get
        {
            IDialogRootContext root = behavior!.Context;
            bool nestedOpen = root.NestedOpenDialogCount > 0;
            if (state.Open != root.Open || state.TransitionStatus != root.TransitionStatus || state.Nested != root.Nested || state.NestedDialogOpen != nestedOpen)
            {
                state = new AlertDialogPopupState(root.Open, root.TransitionStatus, root.Nested, nestedOpen);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<AlertDialogPopupState>? StateAttributesMapping => s_mapping;

    // For the fixtures: completes once the transitions, focus and floating handles are attached.
    internal Task TwinAttached => behavior?.TwinAttached ?? Task.CompletedTask;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (Context is null)
        {
            throw DialogParts.MissingRoot("AlertDialog");
        }

        behavior = new DialogPopupBehavior("AlertDialog", JSRuntime, Logger, StateHasChanged, InvokeAsync, DispatchExceptionAsync);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet() => behavior!.Apply(InitialFocus, InitialFocusResolver, FinalFocus, FinalFocusResolver, RenderedId);

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => behavior!.OnAfterRenderAsync(Element, RenderedId);

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
    protected override void AddPartAttributes(AttributeList attributes) => behavior!.AddAttributes(attributes, HasConsumerId);

    /// <inheritdoc />
    protected override void BuildBeforeElement(RenderTreeBuilder builder) => behavior!.BuildBeforeElement(builder);

    /// <inheritdoc />
    protected override void BuildAfterElement(RenderTreeBuilder builder) => behavior!.BuildAfterElement(builder);
}
