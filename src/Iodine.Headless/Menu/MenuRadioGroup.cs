using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace Blazix.Primitives;

// MNU §3.13 state object (menu/radio-group/MenuRadioGroup.tsx:51-55): Disabled, projected generically (MNU-DOM-29).
/// <summary>Represents the state of a menu radio group, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Disabled">A value indicating whether the group ignores user interaction; projected as <c>data-disabled</c>.</param>
[PartState]
public sealed partial record MenuRadioGroupState(bool Disabled);

// Base UI's Menu.RadioGroup (menu/radio-group/MenuRadioGroup.tsx); the contract is docs/components/menu.md §3.13
// (MNU-API-44..46, ARIA-10, DOM-29, EVT-09). A thin shell over MenuRadioGroupBehavior, on the RadioGroup<TValue> shape (ADR
// 08 Amendments 2026-11-03 (2), chosen by the user): no composite root of its own, no Field/Form wiring.
/// <summary>Groups related radio items of a menu, Base UI's <c>Menu.RadioGroup</c>, rendered as a <c>div</c> with the <c>group</c> role.</summary>
/// <typeparam name="TValue">The type of the group's value and its items' values.</typeparam>
[CascadingTypeParameter(nameof(TValue))]
public sealed class MenuRadioGroup<TValue> : PartBase<MenuRadioGroupState>
{
    private readonly MenuRadioGroupBehavior<TValue> behavior;
    private MenuRadioGroupState state = new(false);
    private MenuFilterGroupContext? filterGroup;

    /// <summary>Initializes a new instance of the <see cref="MenuRadioGroup{TValue}"/> class.</summary>
    public MenuRadioGroup()
    {
        behavior = new MenuRadioGroupBehavior<TValue>("Menu", StateHasChanged);
    }

    [Inject]
    private ILogger<MenuRadioGroup<TValue>> Logger { get; set; } = null!;

    [CascadingParameter]
    private MenuRootContext? RootContext { get; set; }

    [CascadingParameter]
    private MenuFilterGroupContext? FilterGroup { get; set; }

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
    protected override MenuRadioGroupState State
    {
        get
        {
            if (state.Disabled != Disabled)
            {
                state = new MenuRadioGroupState(Disabled);
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
        MenuRootContext.Require(RootContext, "Menu");

        // MNU-STA-24, DOM-20 (M5 track 12): in a filterable menu the group counts its members and hides with them.
        if (RootContext!.Filter is not null)
        {
            filterGroup = new MenuFilterGroupContext(FilterGroup, () => _ = InvokeAsync(StateHasChanged));
        }

        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        behavior.Apply(ValueChanged, OnValueChange, Disabled);
        ChildContent = behavior.CascadeContent(ChildContent);
        if (filterGroup is not null)
        {
            ChildContent = filterGroup.Cascade(ChildContent);
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            filterGroup?.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        behavior.AddAttributes(attributes);

        // MNU-DOM-20: the boolean hidden while the query hides every member; a consumer's hidden is kept.
        if (filterGroup is { Hidden: true })
        {
            attributes.Set("hidden", true);
        }
    }
}
