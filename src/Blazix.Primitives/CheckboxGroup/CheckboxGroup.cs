using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace Blazix.Primitives;

// CKG-STA-01; checkbox-group/CheckboxGroup.tsx:143-146,181-186 (the field's state with the group's disabled state). ADR 08
// §3: the state record is sealed, partial and [PartState].
/// <summary>Represents the state of a checkbox group, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Disabled">A value indicating whether every box of the group is disabled; projected as <c>data-disabled</c>.</param>
/// <param name="Touched">A value indicating whether a box of the group's field has lost focus once; projected as <c>data-touched</c>.</param>
/// <param name="Dirty">A value indicating whether the value differs from the field's initial one; projected as <c>data-dirty</c>.</param>
/// <param name="Valid">
/// The field's validity: <see langword="true"/> projects <c>data-valid</c>, <see langword="false"/> projects
/// <c>data-invalid</c>, and <see langword="null"/>, before validation, projects neither.
/// </param>
/// <param name="Filled">A value indicating whether the group's value is not empty within a field; projected as <c>data-filled</c>.</param>
/// <param name="Focused">A value indicating whether a box of the group has focus within its field; projected as <c>data-focused</c>.</param>
[PartState]
public sealed partial record CheckboxGroupState(bool Disabled, bool Touched, bool Dirty, bool? Valid, bool Filled, bool Focused);

// CKG §3.2; checkbox-group/CheckboxGroupDataAttributes.ts (only `disabled`, as Base UI exports; §16 Q3).
/// <summary>Provides the names of the data attributes a checkbox group renders.</summary>
public static class CheckboxGroupDataAttributes
{
    /// <summary>Represents the attribute present, with an empty value, while the group is disabled: <c>data-disabled</c>.</summary>
    public const string Disabled = "data-disabled";
}

// Base UI's CheckboxGroup (checkbox-group/CheckboxGroup.tsx); the contract is docs/components/checkbox-group.md.
// - One `div` with role="group" and no inputs of its own (CKG-STR-01); the consumer's attributes win (CKG-DOM-05).
// - The value is a Controlled<IReadOnlyList<string>> fixed at the first parameter set (ADR 05 Amendments 2026-10-18;
//   ADR 08 Amendments 2026-10-22: the triple carries one type, a commit produces a new list, equality by reference).
// - The boxes in the subtree read the group through CheckboxGroupContext and run their requests through it: the box's own
//   callback, then the group's with the same details, then the commits (CHK-EVT-09, CKG-EVT-01..05).
// - The registration id is drawn from the generator and never rendered (CKG-API-06; design point G2, pending the user).
/// <summary>
/// Represents a set of checkboxes that share one value, the identities of the ticked boxes: Base UI's
/// <c>CheckboxGroup</c>, rendered as a <c>div</c> with <c>role="group"</c>. A <see cref="CheckboxRoot"/> with
/// <see cref="CheckboxRoot.Parent"/> set ticks or clears all of them when <see cref="AllValues"/> lists them.
/// </summary>
public sealed class CheckboxGroup : PartBase<CheckboxGroupState>
{
    private const string DefaultTag = "div";

    private static readonly IReadOnlyList<string> s_empty = [];
    private static readonly StateAttributesMapping<CheckboxGroupState> s_mapping = FieldValidityMapping.Create<CheckboxGroupState>();

    private Controlled<IReadOnlyList<string>>? valueState;
    private CheckboxGroupContext? context;
    private CheckboxGroupState? state;
    private string? registrationId;
    private RenderFragment? cascade;
    private RenderFragment? cascadedContent;

    [Inject]
    private ILogger<CheckboxGroup> Logger { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    // CKG-API-02, CKG-STA-04, CKG-EDGE-03: a null default, or a controlled value removed later, reads as an empty list.
    private IReadOnlyList<string> CurrentValue => valueState!.Value ?? s_empty;

    private bool EffectiveDisabled => Disabled;

    // CKG-API-01, CKG-STA-02/04; ADR 05 §2, ADR 08 Amendments 2026-10-22
    /// <summary>Gets or sets the identities of the ticked boxes, when the consumer owns the value.</summary>
    /// <value>
    /// The value; an array or a list binds. Supplying it at the first render makes the value the consumer's for the
    /// lifetime of the group: a toggle then only raises <see cref="OnValueChange"/> and <see cref="ValueChanged"/>, each
    /// with a new list.
    /// </value>
    [Parameter]
    public IReadOnlyList<string>? Value { get; set; }

    // ADR 05 §2: the binding half of @bind-Value.
    /// <summary>Gets or sets the callback that receives the new value after a toggle that was not canceled.</summary>
    /// <value>The callback <c>@bind-Value</c> supplies, invoked after <see cref="OnValueChange"/>.</value>
    [Parameter]
    public EventCallback<IReadOnlyList<string>> ValueChanged { get; set; }

    // CKG-API-02
    /// <summary>Gets or sets the identities of the boxes initially ticked, when the group owns the value.</summary>
    /// <value>The value; <see langword="null"/>, the default, is an empty list. Read at the first render only.</value>
    [Parameter]
    public IReadOnlyList<string>? DefaultValue { get; set; }

    // CKG-API-03, CKG-EVT-01..07
    /// <summary>Gets or sets the callback invoked when a box of the group is toggled, before the value changes.</summary>
    /// <value>
    /// The callback, which receives the proposed value and the change details the toggled box received. Calling
    /// <see cref="ChangeEventDetails.Cancel"/> keeps the current value.
    /// </value>
    [Parameter]
    public EventCallback<ValueChangeEventArgs<IReadOnlyList<string>>> OnValueChange { get; set; }

    // CKG-API-04
    /// <summary>Gets or sets the identities of all boxes of the group, which enables its parent box.</summary>
    /// <value>
    /// The list a parent box ticks when it selects all, and the order of its <c>aria-controls</c>; or
    /// <see langword="null"/>, the default, for no parent behavior. An empty list is a list.
    /// </value>
    [Parameter]
    public IReadOnlyList<string>? AllValues { get; set; }

    // CKG-API-05
    /// <summary>Gets or sets a value indicating whether every box of the group is disabled.</summary>
    /// <value><see langword="true"/> to disable every box, whatever its own setting; the default is <see langword="false"/>.</value>
    [Parameter]
    public bool Disabled { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override CheckboxGroupState State => state!;

    /// <inheritdoc />
    protected override StateAttributesMapping<CheckboxGroupState>? StateAttributesMapping => s_mapping;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        // CKG-STA-02, CKG-EDGE-01/02: ownership is fixed at the first parameter set.
        valueState ??= new Controlled<IReadOnlyList<string>>("CheckboxGroup", "value", Logger);
        valueState.SetParameters(parameters, nameof(Value), nameof(DefaultValue));
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        base.OnInitialized();

        // Design point G2: the registration id, drawn in render order and never rendered (CKG-API-06).
        registrationId = IdGenerator.Next();
        context = new CheckboxGroupContext(CurrentValue, RequestAsync);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        UpdateState();
        context!.SetState(CurrentValue, AllValues, EffectiveDisabled);

        // The cascade wraps the consumer's content inside the group's element; it is rebuilt only when the consumer
        // supplied other content (the Meter precedent).
        if (cascade is null || (!ReferenceEquals(ChildContent, cascade) && !ReferenceEquals(cascadedContent, ChildContent)))
        {
            cascadedContent = ChildContent;
            cascade = context.Cascade(cascadedContent);
        }

        ChildContent = cascade;
    }

    // CKG-ARIA-01: the consumer's role wins at the merge.
    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        attributes.Set("role", "group");
    }

    // CHK-EVT-09, CKG-EVT-01..05: the group's half of a box's request, with the box's details.
    private async Task<bool> RequestAsync(IReadOnlyList<string> next, ChangeEventDetails details)
    {
        if (!await valueState!.NotifyAsync(next, details, OnValueChange))
        {
            return false;
        }

        await valueState.CommitAsync(next, ValueChanged);
        context!.SetState(CurrentValue, AllValues, EffectiveDisabled);
        if (UpdateState())
        {
            StateHasChanged();
        }

        return true;
    }

    // Returns whether the state changed; a new record is made only then (ADR 16 rule 7).
    private bool UpdateState()
    {
        bool disabled = EffectiveDisabled;
        if (state is { } current && current.Disabled == disabled)
        {
            return false;
        }

        state = new CheckboxGroupState(disabled, Touched: false, Dirty: false, Valid: null, Filled: false, Focused: false);
        return true;
    }
}
