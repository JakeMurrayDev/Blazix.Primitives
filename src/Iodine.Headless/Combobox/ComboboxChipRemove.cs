using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// CMB §3.6, STA-04 (combobox/chip-remove/ComboboxChipRemove.tsx).
/// <summary>Represents the state of a combobox chip remove button, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Disabled">A value indicating whether the button is disabled, by the part or the root; projected as <c>data-disabled</c>.</param>
[PartState]
public sealed partial record ComboboxChipRemoveState(
    bool Disabled);

// Base UI's Combobox.ChipRemove (combobox/chip-remove/ComboboxChipRemove.tsx; CMB §3.6). Over ComboboxChipRemoveBehavior.
/// <summary>Represents the button that removes a combobox chip's value, Base UI's <c>Combobox.ChipRemove</c>, rendered as a <c>button</c>.</summary>
/// <remarks>It is not a tab stop; a click, Enter or Space removes the chip's value and moves the focus to the input.</remarks>
public sealed class ComboboxChipRemove : PartBase<ComboboxChipRemoveState>
{
    private readonly EventCallback onKeyDown;

    private readonly EventCallback onKeyUp;

    private ComboboxChipRemoveBehavior? behavior;

    private ComboboxChipRemoveState state = new(false);

    /// <summary>Initializes a new instance of the <see cref="ComboboxChipRemove"/> class.</summary>
    public ComboboxChipRemove()
    {
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", e => behavior!.HandleKeyDownAsync(e));
        onKeyUp = CreateHandler<KeyboardEventArgs>("onkeyup", e => behavior!.HandleKeyUpAsync(e));
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<ComboboxChipRemove> Logger { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    [Inject]
    private ButtonDevelopmentLog DevelopmentLog { get; set; } = null!;

    [CascadingParameter]
    private ComboboxStore? Root { get; set; }

    [CascadingParameter]
    private ComboboxChipContext? Chip { get; set; }

    /// <summary>Gets or sets a value indicating whether the rendered element is a native <c>button</c>; the default is <see langword="true"/>.</summary>
    [Parameter]
    public bool NativeButton { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the button is disabled; the combobox's disabled state also applies.</summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "button";

    /// <inheritdoc />
    protected override ComboboxChipRemoveState State
    {
        get
        {
            bool disabled = behavior!.Disabled;
            if (state.Disabled != disabled)
            {
                state = new ComboboxChipRemoveState(disabled);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        behavior = new ComboboxChipRemoveBehavior(new ComboboxPartServices("Combobox", JSRuntime, Logger, IdGenerator, StateHasChanged, InvokeAsync, DispatchExceptionAsync), DevelopmentLog, onKeyDown, onKeyUp);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet() => behavior!.Apply(NativeButton, Disabled, AdditionalAttributes);

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => behavior!.AfterRenderAsync(Element, Id);

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
    protected override void SubscribeSelectors() => behavior!.Subscribe(Root, Chip, SubscribeTo);

    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName) => behavior!.ForwardsConsumerHandler(eventName);

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes) => behavior!.AddPartAttributes(attributes, Id);
}
