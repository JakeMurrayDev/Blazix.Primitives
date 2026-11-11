using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// CMB §3.25, STA-14 (combobox/item-indicator/ComboboxItemIndicator.tsx; ItemIndicatorBehavior): the status mirrored.
/// <summary>Represents the state of a combobox item indicator, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Selected">A value indicating whether the item is selected; projected as <c>data-selected</c>.</param>
/// <param name="TransitionStatus">The enter or exit status; its <c>data-starting-style</c> and <c>data-ending-style</c> are written by the browser-side behavior.</param>
[PartState]
public sealed partial record ComboboxItemIndicatorState(
    bool Selected,
    [property: MirroredField] TransitionStatus? TransitionStatus);

// combobox/item-indicator/ComboboxItemIndicatorDataAttributes.ts (CMB-DOM-10).
/// <summary>Provides the names of the data attributes a combobox item indicator renders.</summary>
public static class ComboboxItemIndicatorDataAttributes
{
    /// <summary>Represents the attribute present while the indicator animates in: <c>data-starting-style</c>.</summary>
    public const string StartingStyle = PopupDataAttributes.StartingStyle;

    /// <summary>Represents the attribute present while the indicator animates out: <c>data-ending-style</c>.</summary>
    public const string EndingStyle = PopupDataAttributes.EndingStyle;
}

// Base UI's Combobox.ItemIndicator (combobox/item-indicator/ComboboxItemIndicator.tsx; CMB §3.25). Over
// ComboboxItemIndicatorBehavior, which composes the shared ItemIndicatorBehavior: rendered while its item is selected, through
// its exit, or always with KeepMounted; aria-hidden; the default content is a check mark.
/// <summary>Represents the indicator that a combobox item is selected, Base UI's <c>Combobox.ItemIndicator</c>, rendered as a <c>span</c>.</summary>
public sealed class ComboboxItemIndicator : PartBase<ComboboxItemIndicatorState>
{
    private ComboboxItemIndicatorBehavior? behavior;

    private ComboboxItemIndicatorState state = new(false, null);

    private bool applied;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<ComboboxItemIndicator> Logger { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    [CascadingParameter]
    private ComboboxItemContext? Item { get; set; }

    /// <summary>Gets or sets a value indicating whether the indicator stays in the DOM while its item is not selected.</summary>
    [Parameter]
    public bool KeepMounted { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "span";

    /// <inheritdoc />
    protected override ComboboxItemIndicatorState State
    {
        get
        {
            ComboboxItemIndicatorBehavior indicator = behavior!;
            if (state.Selected != indicator.Selected || state.TransitionStatus != indicator.TransitionStatus)
            {
                state = new ComboboxItemIndicatorState(indicator.Selected, indicator.TransitionStatus);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        behavior = new ComboboxItemIndicatorBehavior(new ComboboxPartServices("Combobox", JSRuntime, Logger, IdGenerator, StateHasChanged, InvokeAsync, DispatchExceptionAsync));
        behavior.Subscribe(Item);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        ChildContent ??= ComboboxItemIndicatorBehavior.DefaultContent;
        behavior!.Apply(KeepMounted, !applied);
        applied = true;
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (!behavior!.Shown)
        {
            ((IPartHost)this).SetElement(null);
            return;
        }

        base.BuildRenderTree(builder);
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => behavior!.AfterRenderAsync(behavior.Shown ? Element : null);

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
    protected override void AddPartAttributes(AttributeList attributes) => behavior!.AddPartAttributes(attributes);
}
