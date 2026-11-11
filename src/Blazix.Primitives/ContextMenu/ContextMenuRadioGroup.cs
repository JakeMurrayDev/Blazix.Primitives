using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace Blazix.Primitives;

// MNU §3.13 state object (menu/radio-group/MenuRadioGroup.tsx:51-55): Disabled, projected generically (MNU-DOM-29).
/// <summary>Represents the state of a context menu radio group, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Disabled">A value indicating whether the group ignores user interaction; projected as <c>data-disabled</c>.</param>
[PartState]
public sealed partial record ContextMenuRadioGroupState(bool Disabled);

// Base UI's ContextMenu.RadioGroup is Menu.RadioGroup (context-menu/index.parts.ts:4-19; CTX-STR-01, STR-02: the MNU rows below are
// adopted). Decision 12 of the M5 plan and ADR 08 Amendments 2026-11-03 (3) (chosen by the user 2026-11-03): a sealed shell
// over Menu's behaviour with the family name `ContextMenu`, which its diagnostics name (CTX-EDGE-02); M5 track 7.
// Base UI's Menu.RadioGroup (menu/radio-group/MenuRadioGroup.tsx); the contract is docs/components/menu.md §3.13
// (MNU-API-44..46, ARIA-10, DOM-29, EVT-09). A thin shell over MenuRadioGroupBehavior, on the RadioGroup<TValue> shape (ADR
// 08 Amendments 2026-11-03 (2), chosen by the user): no composite root of its own, no Field/Form wiring.
/// <summary>Groups related radio items of a context menu, Base UI's <c>ContextMenu.RadioGroup</c>, rendered as a <c>div</c> with the <c>group</c> role.</summary>
/// <typeparam name="TValue">The type of the group's value and its items' values.</typeparam>
[CascadingTypeParameter(nameof(TValue))]
public sealed class ContextMenuRadioGroup<TValue> : PartBase<ContextMenuRadioGroupState>
{
    private readonly MenuRadioGroupBehavior<TValue> behavior;
    private ContextMenuRadioGroupState state = new(false);

    /// <summary>Initializes a new instance of the <see cref="ContextMenuRadioGroup{TValue}"/> class.</summary>
    public ContextMenuRadioGroup()
    {
        behavior = new MenuRadioGroupBehavior<TValue>("ContextMenu", StateHasChanged);
    }

    [Inject]
    private ILogger<ContextMenuRadioGroup<TValue>> Logger { get; set; } = null!;

    [CascadingParameter]
    private MenuRootContext? RootContext { get; set; }

    // MNU-API-44
    /// <summary>Gets or sets the value of the group's selected item.</summary>
    /// <value>
    /// The value the consumer owns, when supplied at the first render; then a selection only reports through
    /// <see cref="OnValueChange"/> and <see cref="ValueChanged"/>. Use <c>@bind-Value</c> to follow them.
    /// </value>
    [Parameter]
    public TValue Value { get; set; } = default!;

    /// <summary>Gets or sets the callback that receives the requested value, for <c>@bind-Value</c>.</summary>
    /// <value>Invoked after <see cref="OnValueChange"/> when the change is not canceled.</value>
    [Parameter]
    public EventCallback<TValue> ValueChanged { get; set; }

    /// <summary>Gets or sets the value of the item selected at the first render, while <see cref="Value"/> is not supplied.</summary>
    /// <value>The type's default by default.</value>
    [Parameter]
    public TValue DefaultValue { get; set; } = default!;

    // MNU-API-45
    /// <summary>Gets or sets the callback invoked when an item of the group is activated, even when it is already selected.</summary>
    /// <value>Receives the item's value and its details; calling <c>Details.Cancel()</c> keeps the current value.</value>
    [Parameter]
    public EventCallback<ValueChangeEventArgs<TValue>> OnValueChange { get; set; }

    // MNU-API-46
    /// <summary>Gets or sets a value indicating whether the group and its items ignore user interaction.</summary>
    /// <value><see langword="false"/> by default.</value>
    [Parameter]
    public bool Disabled { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override ContextMenuRadioGroupState State
    {
        get
        {
            if (state.Disabled != Disabled)
            {
                state = new ContextMenuRadioGroupState(Disabled);
            }

            return state;
        }
    }

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        // The part replaces ChildContent with its own cascade, so a parameter set without content must not keep it.
        ChildContent = null;
        behavior.SetParameters(parameters, Logger);
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The group is outside a menu.</exception>
    protected override void OnInitialized()
    {
        MenuRootContext.Require(RootContext, "ContextMenu");
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        behavior.Apply(ValueChanged, OnValueChange, Disabled);
        ChildContent = behavior.CascadeContent(ChildContent);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes) => behavior.AddAttributes(attributes);
}
