using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// DLG §3.6 state object (dialog/popup/DialogPopup.tsx:64-69). TransitionStatus is mirrored (ADR 04 §2).
/// <summary>Represents the state of a dialog's popup, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Open">A value indicating whether the dialog is open; projected as <c>data-open</c> or <c>data-closed</c>.</param>
/// <param name="TransitionStatus">
/// The phase of the dialog's enter or exit transition, or <see langword="null"/> while none is in progress; its
/// <c>data-starting-style</c> and <c>data-ending-style</c> attributes are written by the browser-side behavior.
/// </param>
/// <param name="Nested">A value indicating whether the dialog is nested in another dialog; projected as <c>data-nested</c>.</param>
/// <param name="NestedDialogOpen">A value indicating whether a dialog nested in this one is open; projected as <c>data-nested-dialog-open</c>.</param>
[PartState]
public sealed partial record DialogPopupState(bool Open, [property: MirroredField] TransitionStatus? TransitionStatus, bool Nested, bool NestedDialogOpen);

// dialog/popup/DialogPopupDataAttributes.ts (DLG-DOM-20..22).
/// <summary>Provides the names of the data attributes a dialog's popup renders.</summary>
public static class DialogPopupDataAttributes
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

// dialog/popup/DialogPopupCssVars.ts (DLG-DOM-19).
/// <summary>Provides the names of the CSS variables a dialog's popup sets.</summary>
public static class DialogPopupCssVars
{
    /// <summary>Represents the number of open dialogs nested in this one: <c>--nested-dialogs</c>.</summary>
    public const string NestedDialogs = "--nested-dialogs";
}

// Base UI's Dialog.Popup (dialog/popup/DialogPopup.tsx); the contract is docs/components/dialog.md (§3.6, §5.5, §6). A thin
// part over DialogPopupBehavior, which AlertDialogPopup shares (ADR 08 Amendments 2026-10-27).
/// <summary>
/// Represents a container for the dialog's contents, Base UI's <c>Dialog.Popup</c>, rendered as a <c>div</c> with the
/// <c>dialog</c> role.
/// </summary>
/// <remarks>
/// Place it inside the dialog's portal. Focus moves into it when the dialog opens and returns to the trigger when it closes.
/// </remarks>
public sealed class DialogPopup : PartBase<DialogPopupState>
{
    private static readonly StateDataAttribute[] s_nestedDialogOpen = [new(DialogPopupDataAttributes.NestedDialogOpen, string.Empty)];

    // dialog/utils/stateAttributesMapping.ts: the popup's open mapping, and nestedDialogOpen as data-nested-dialog-open
    // (Nested keeps the default projection, data-nested).
    private static readonly StateAttributesMapping<DialogPopupState> s_mapping = PopupStateMapping.CreateOpen<DialogPopupState>()
        .Map<bool>(nameof(DialogPopupState.NestedDialogOpen), static open => open ? s_nestedDialogOpen : null);

    private DialogPopupBehavior? behavior;
    private DialogPopupState state = new(false, null, false, false);

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<DialogPopup> Logger { get; set; } = null!;

    private bool HasConsumerId => AdditionalAttributes is not null && AdditionalAttributes.ContainsKey("id");

    private string? RenderedId => HasConsumerId ? AdditionalAttributes!["id"] as string : Context!.FloatingId;

    [CascadingParameter]
    private IDialogRootContext? Context { get; set; }

    [CascadingParameter]
    private PopupPortalContext? Portal { get; set; }

    // DLG-API-20
    /// <summary>Gets or sets where focus moves when the dialog opens.</summary>
    /// <value>
    /// <see langword="null"/>, the default, for the first tabbable element inside the popup, or the popup itself when the
    /// dialog was opened by touch; <see langword="true"/> for the first tabbable element; <see langword="false"/> to leave
    /// focus where it is; an element or the id of an element to focus it.
    /// </value>
    [Parameter]
    public FocusTargetValue? InitialFocus { get; set; }

    /// <summary>Gets or sets a function that decides where focus moves when the dialog opens, from how it was opened.</summary>
    /// <value>
    /// A function called once per open with the interaction type (<see cref="InteractionType.Unknown"/> when it is not
    /// known). Its result is used as <see cref="InitialFocus"/> would be, except that <see langword="null"/> leaves focus
    /// where it is. When it is set, <see cref="InitialFocus"/> is ignored.
    /// </value>
    [Parameter]
    public Func<InteractionType, FocusTargetValue?>? InitialFocusResolver { get; set; }

    // DLG-API-21
    /// <summary>Gets or sets where focus moves when the dialog closes.</summary>
    /// <value>
    /// <see langword="null"/>, the default, or <see langword="true"/> for the trigger, else the element focused before the
    /// dialog opened; <see langword="false"/> to leave focus where it is; an element or the id of an element to focus it.
    /// </value>
    [Parameter]
    public FocusTargetValue? FinalFocus { get; set; }

    /// <summary>Gets or sets a function that decides where focus moves when the dialog closes, from how it was closed.</summary>
    /// <value>
    /// A function called once per close with the interaction type. Its result is used as <see cref="FinalFocus"/> would be,
    /// except that <see langword="null"/> leaves focus where it is. When it is set, <see cref="FinalFocus"/> is ignored.
    /// </value>
    [Parameter]
    public Func<InteractionType, FocusTargetValue?>? FinalFocusResolver { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override DialogPopupState State
    {
        get
        {
            IDialogRootContext root = behavior!.Context;
            bool nestedOpen = root.NestedOpenDialogCount > 0;
            if (state.Open != root.Open || state.TransitionStatus != root.TransitionStatus || state.Nested != root.Nested || state.NestedDialogOpen != nestedOpen)
            {
                state = new DialogPopupState(root.Open, root.TransitionStatus, root.Nested, nestedOpen);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<DialogPopupState>? StateAttributesMapping => s_mapping;

    // For the fixtures: completes once the transitions, focus and floating handles are attached.
    internal Task TwinAttached => behavior?.TwinAttached ?? Task.CompletedTask;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (Context is null)
        {
            throw DialogParts.MissingRoot("Dialog");
        }

        behavior = new DialogPopupBehavior("DialogPopup", JSRuntime, Logger, StateHasChanged, InvokeAsync, DispatchExceptionAsync);
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
