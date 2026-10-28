using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Fixtures.Client.FocusManagerFixtures;

/// <summary>
/// A test-only focus-managed popup for the <c>focus-manager-*</c> fixtures (tests/browser/focus-manager.spec.ts): a
/// <c>div</c> with the Popup parts' focusable props (<c>role="dialog"</c>, <c>tabindex="-1"</c>,
/// <c>data-base-ui-focusable</c>) and <c>data-open</c> while <see cref="Open"/>, composing
/// <see cref="FocusManagerBehavior"/> as a Popup part does: the inside guards around it, the publication to an enclosing
/// portal, the inputs from the parameter set. No public part manages focus before Popover and Dialog (the M2 and M3
/// composite precedent: ADR 12 Amendments 2026-10-27 item 1).
/// </summary>
public sealed class FixtureFocusManaged : PartBase<EmptyState>
{
    private FocusManagerBehavior? behavior;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private PopupPortalContext? Portal { get; set; }

    [Parameter]
    public bool Open { get; set; }

    [Parameter]
    public bool Modal { get; set; } = true;

    [Parameter]
    public bool Disabled { get; set; }

    [Parameter]
    public bool InitialFocus { get; set; } = true;

    [Parameter]
    public string? InitialFocusId { get; set; }

    [Parameter]
    public bool ReturnFocus { get; set; } = true;

    [Parameter]
    public string? ReturnFocusId { get; set; }

    [Parameter]
    public bool? ExplicitReturnFocus { get; set; }

    [Parameter]
    public string RestoreFocus { get; set; } = FocusManagerTwin.RestoreNone;

    [Parameter]
    public bool CloseOnFocusOut { get; set; } = true;

    /// <summary>Gets or sets how the popup was opened; <see langword="null"/> for a programmatic open.</summary>
    [Parameter]
    public InteractionType? OpenInteractionType { get; set; } = InteractionType.Unknown;

    /// <summary>Gets or sets the snapshot of the event that caused the last close, which the close type is classified from.</summary>
    [Parameter]
    public EventSnapshot? CloseEvent { get; set; }

    [Parameter]
    public string? DomReferenceId { get; set; }

    [Parameter]
    public string? OpenSelector { get; set; }

    [Parameter]
    public bool TreatPopupAsFloatingElement { get; set; } = true;

    [Parameter]
    public bool TriggerFocusGuards { get; set; }

    [Parameter]
    public IReadOnlyList<string>? InsideElementIds { get; set; }

    /// <summary>Gets or sets a value indicating whether the popup renders <c>tabindex="-1"</c>, as every Popup part does.</summary>
    [Parameter]
    public bool RenderTabIndex { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the inputs are set from the parameter set, so changed options are sent
    /// before the render that applies them (design point 1 (d)), rather than from <c>OnAfterRender</c>.
    /// </summary>
    [Parameter]
    public bool PushBeforeRender { get; set; } = true;

    /// <summary>Gets or sets the callback the focus-out close request reaches; the behavior answers every request.</summary>
    [Parameter]
    public EventCallback<EventSnapshot> OnFocusOutClose { get; set; }

    public Task TwinAttached => behavior?.TwinAttached ?? Task.CompletedTask;

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override EmptyState State => EmptyState.Instance;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        behavior = new FocusManagerBehavior(JSRuntime, InvokeAsync, snapshot => OnFocusOutClose.InvokeAsync(snapshot));
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        behavior!.Portal = Portal;
        if (PushBeforeRender)
        {
            behavior.SetInputs(Inputs());
        }
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!RendererInfo.IsInteractive)
        {
            return;
        }

        if (!PushBeforeRender)
        {
            behavior!.SetInputs(Inputs());
        }

        bool wasAttached = behavior!.TwinAttached.IsCompleted;
        await behavior.AfterRenderAsync(Element, Id);
        if (!wasAttached && behavior.TwinAttached.IsCompleted)
        {
            // data-handle-attached.
            StateHasChanged();
        }
    }

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
    protected override void AddPartAttributes(AttributeList attributes)
    {
        attributes.Set("role", "dialog");
        if (RenderTabIndex)
        {
            attributes.Set("tabindex", "-1");
        }

        attributes.Set("data-base-ui-focusable", "");
        if (behavior!.TwinAttached.IsCompleted)
        {
            attributes.Set("data-handle-attached", "");
        }

        if (Open)
        {
            attributes.Set("data-open", "");
        }
    }

    /// <inheritdoc />
    protected override void BuildBeforeElement(RenderTreeBuilder builder) => behavior!.BuildBeforeElement(builder);

    /// <inheritdoc />
    protected override void BuildAfterElement(RenderTreeBuilder builder) => behavior!.BuildAfterElement(builder);

    private FocusManagerInputs Inputs() => new()
    {
        Open = Open,
        Modal = Modal,
        Disabled = Disabled,
        InitialFocus = Target(InitialFocus, InitialFocusId),
        ReturnFocus = Target(ReturnFocus, ReturnFocusId),
        ExplicitReturnFocus = ExplicitReturnFocus,
        RestoreFocus = RestoreFocus,
        CloseOnFocusOut = CloseOnFocusOut,
        OpenInteractionType = OpenInteractionType,
        CloseEvent = CloseEvent,
        OpenSelector = OpenSelector,
        TreatPopupAsFloatingElement = TreatPopupAsFloatingElement,
        DomReferenceId = DomReferenceId,
        TriggerFocusGuards = TriggerFocusGuards,
        InsideElementIds = InsideElementIds,
    };

    // The value union as a family resolves it: false moves no focus, an id targets that element, else the default.
    private static FocusTargetValue? Target(bool focus, string? id)
    {
        if (!focus)
        {
            return new FocusTargetValue(false);
        }

        if (id is null)
        {
            // Not `null` in a conditional: the string conversion would take it.
            return default(FocusTargetValue?);
        }

        return new FocusTargetValue(id);
    }
}
