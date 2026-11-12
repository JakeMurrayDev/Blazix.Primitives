using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// AUT-STR-02, STR-05 (M5 track 11; decision 12, ADR 08 Amendments 2026-11-03 (3)): the Autocomplete parts of this file
// are Combobox's, as sealed shells over the same internal behaviours with the family `Autocomplete`; the comments below
// cite the Combobox rows the Autocomplete document adopts (autocomplete.md §2.1).

// CMB §3.13, STA-08 (combobox/backdrop/AutocompleteBackdrop.tsx): the popup transition mapping; the status mirrored.
/// <summary>Represents the state of an autocomplete backdrop, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Open">A value indicating whether the popup is open; projected as <c>data-open</c> or <c>data-closed</c>.</param>
/// <param name="TransitionStatus">The enter or exit status; its <c>data-starting-style</c> and <c>data-ending-style</c> are written by the browser-side behavior.</param>
[PartState]
public sealed partial record AutocompleteBackdropState(
    bool Open,
    [property: MirroredField] TransitionStatus? TransitionStatus);

// combobox/backdrop/AutocompleteBackdropDataAttributes.ts (CMB-DOM-14).
/// <summary>Provides the names of the data attributes an autocomplete backdrop renders.</summary>
public static class AutocompleteBackdropDataAttributes
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

// Base UI's Combobox.Backdrop (combobox/backdrop/AutocompleteBackdrop.tsx; CMB §3.13). Over ComboboxBackdropBehavior:
// role="presentation", hidden while the popup is unmounted, user-select: none under the consumer's style, its own
// TransitionsTwin writing the status attributes.
/// <summary>Represents an overlay beneath an autocomplete's popup, Base UI's <c>Autocomplete.Backdrop</c>, rendered as a <c>div</c>.</summary>
public sealed class AutocompleteBackdrop : PartBase<AutocompleteBackdropState>
{
    private static readonly StateAttributesMapping<AutocompleteBackdropState> s_mapping = PopupStateMapping.CreateOpen<AutocompleteBackdropState>();

    private ComboboxBackdropBehavior? behavior;

    private AutocompleteBackdropState state = new(false, null);

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<AutocompleteBackdrop> Logger { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    [CascadingParameter]
    private ComboboxStore? Root { get; set; }

    [CascadingParameter]
    private PopupPortalContext? Portal { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override AutocompleteBackdropState State
    {
        get
        {
            ComboboxBackdropBehavior backdrop = behavior!;
            bool open = backdrop.Store.Lifecycle.EffectiveOpen;
            TransitionStatus? status = backdrop.TransitionStatus;
            if (state.Open != open || state.TransitionStatus != status)
            {
                state = new AutocompleteBackdropState(open, status);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<AutocompleteBackdropState>? StateAttributesMapping => s_mapping;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        behavior = new ComboboxBackdropBehavior(new ComboboxPartServices("Autocomplete", JSRuntime, Logger, IdGenerator, StateHasChanged, InvokeAsync, DispatchExceptionAsync));
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
