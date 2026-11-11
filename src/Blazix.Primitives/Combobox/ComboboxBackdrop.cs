using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// CMB §3.13, STA-08 (combobox/backdrop/ComboboxBackdrop.tsx): the popup transition mapping; the status mirrored.
/// <summary>Represents the state of a combobox backdrop, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Open">A value indicating whether the popup is open; projected as <c>data-open</c> or <c>data-closed</c>.</param>
/// <param name="TransitionStatus">The enter or exit status; its <c>data-starting-style</c> and <c>data-ending-style</c> are written by the browser-side behavior.</param>
[PartState]
public sealed partial record ComboboxBackdropState(
    bool Open,
    [property: MirroredField] TransitionStatus? TransitionStatus);

// combobox/backdrop/ComboboxBackdropDataAttributes.ts (CMB-DOM-14).
/// <summary>Provides the names of the data attributes a combobox backdrop renders.</summary>
public static class ComboboxBackdropDataAttributes
{
    /// <summary>Represents the attribute present while the popup is open: <c>data-open</c>.</summary>
    public const string Open = PopupDataAttributes.Open;

    /// <summary>Represents the attribute present while the popup is closed: <c>data-closed</c>.</summary>
    public const string Closed = PopupDataAttributes.Closed;

    /// <summary>Represents the attribute present while the backdrop animates in: <c>data-starting-style</c>.</summary>
    public const string StartingStyle = PopupDataAttributes.StartingStyle;

    /// <summary>Represents the attribute present while the backdrop animates out: <c>data-ending-style</c>.</summary>
    public const string EndingStyle = PopupDataAttributes.EndingStyle;
}

// Base UI's Combobox.Backdrop (combobox/backdrop/ComboboxBackdrop.tsx; CMB §3.13). Over ComboboxBackdropBehavior:
// role="presentation", hidden while the popup is unmounted, user-select: none under the consumer's style, its own
// TransitionsTwin writing the status attributes.
/// <summary>Represents an overlay beneath a combobox's popup, Base UI's <c>Combobox.Backdrop</c>, rendered as a <c>div</c>.</summary>
public sealed class ComboboxBackdrop : PartBase<ComboboxBackdropState>
{
    private static readonly StateAttributesMapping<ComboboxBackdropState> s_mapping = PopupStateMapping.CreateOpen<ComboboxBackdropState>();

    private ComboboxBackdropBehavior? behavior;

    private ComboboxBackdropState state = new(false, null);

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<ComboboxBackdrop> Logger { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    [CascadingParameter]
    private ComboboxStore? Root { get; set; }

    [CascadingParameter]
    private PopupPortalContext? Portal { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override ComboboxBackdropState State
    {
        get
        {
            ComboboxBackdropBehavior backdrop = behavior!;
            bool open = backdrop.Store.Lifecycle.EffectiveOpen;
            TransitionStatus? status = backdrop.TransitionStatus;
            if (state.Open != open || state.TransitionStatus != status)
            {
                state = new ComboboxBackdropState(open, status);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<ComboboxBackdropState>? StateAttributesMapping => s_mapping;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        behavior = new ComboboxBackdropBehavior(new ComboboxPartServices("Combobox", JSRuntime, Logger, IdGenerator, StateHasChanged, InvokeAsync, DispatchExceptionAsync));
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => behavior!.AfterRenderAsync(Element);

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
    protected override void SubscribeSelectors() => behavior!.Subscribe(Root, Portal, SubscribeTo);

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes) => behavior!.AddPartAttributes(attributes);
}
