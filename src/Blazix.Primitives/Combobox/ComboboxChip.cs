using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// CMB §3.5, STA-03 (combobox/chip/ComboboxChip.tsx).
/// <summary>Represents the state of a combobox chip, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Disabled">A value indicating whether the combobox is disabled; projected as <c>data-disabled</c>.</param>
[PartState]
public sealed partial record ComboboxChipState(
    bool Disabled);

// Base UI's Combobox.Chip (combobox/chip/ComboboxChip.tsx; CMB §3.5). Over ComboboxChipBehavior.
/// <summary>Represents one selected value of a multiple combobox, Base UI's <c>Combobox.Chip</c>, rendered as a <c>div</c>.</summary>
public sealed class ComboboxChip : PartBase<ComboboxChipState>
{
    private ComboboxChipBehavior? behavior;

    private ComboboxChipState state = new(false);

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<ComboboxChip> Logger { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    [CascadingParameter]
    private ComboboxStore? Root { get; set; }

    [CascadingParameter]
    private ComboboxChipsContext? Chips { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override ComboboxChipState State
    {
        get
        {
            bool disabled = behavior!.Disabled;
            if (state.Disabled != disabled)
            {
                state = new ComboboxChipState(disabled);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        // The part replaces ChildContent with its own cascade, so a parameter set without content must not keep it.
        ChildContent = null;
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        behavior = new ComboboxChipBehavior(new ComboboxPartServices("Combobox", JSRuntime, Logger, IdGenerator, StateHasChanged, InvokeAsync, DispatchExceptionAsync));
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        ComboboxChipBehavior chip = behavior!;
        chip.Apply(Id);
        RenderFragment? content = ChildContent;
        ChildContent = builder => chip.BuildContent(builder, content);
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => behavior!.AfterRenderAsync(Element);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            behavior?.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void SubscribeSelectors() => behavior!.Subscribe(Root, Chips, SubscribeTo);

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes) => behavior!.AddPartAttributes(attributes);
}
