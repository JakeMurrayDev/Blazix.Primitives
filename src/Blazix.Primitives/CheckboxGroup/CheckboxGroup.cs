using System.Linq.Expressions;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
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
// - The registration id is the consumer's id, else one drawn from the generator and never rendered (CKG-API-06; design
//   point G2, settled by the orchestrator 2026-10-23).
// - Inside a field the group is the field's control (checkbox-group §8): it claims its scope's control id as null, so the
//   label names the group (CKG-ARIA-02/03); it registers its value while the field has a name and it is enabled
//   (CKG-FRM-01/02), with the members' checked, eligible inputs as the form projection (CKG-FRM-03); and after the render
//   that shows a new value it writes filled, clears the form's error, writes dirty and validates the change once every
//   member's input has reported again (CKG-STA-11/12, CKG-FRM-07/08/10). ADR05-5: EditContext participation through
//   ValueExpression, whose identifier's name names a nameless field's registration.
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
    private FieldRootContext? fieldScope;
    private LabelableScope? scope;
    private EditContextParticipation? participation;
    private Action? fieldChanged;
    private Func<object?>? getValue;
    private IReadOnlyList<string>? observedValue;
    private FieldControlRegistration? sentRegistration;
    private bool registered;
    private bool claimRegistered;
    private string? renderedLabelId;
    private string? renderedDescribedBy;
    private bool disposed;

    [Inject]
    private ILogger<CheckboxGroup> Logger { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    // CKG-API-02, CKG-STA-04, CKG-EDGE-03: a null default, or a controlled value removed later, reads as an empty list.
    private IReadOnlyList<string> CurrentValue => valueState!.Value ?? s_empty;

    // CKG-API-05, FFI-STA-11: the field's disabled state with the group's own.
    private bool EffectiveDisabled => Disabled || (fieldScope?.Disabled ?? false);

    // CKG-FRM-01: the field's name, else (ADR05-5) the EditContext identifier's field name, while the group is enabled.
    private bool ShouldRegister => !EffectiveDisabled && (fieldScope!.Name ?? participation!.FieldName) is not null;

    private string? IdParameter => AdditionalAttributes is not null
        && AdditionalAttributes.TryGetValue("id", out object? value)
        && value is string { Length: > 0 } text ? text : null;

    [CascadingParameter]
    private FieldRootContext? FieldContext { get; set; }

    [CascadingParameter]
    private FieldItemContext? ItemContext { get; set; }

    [CascadingParameter]
    private EditContext? CascadedEditContext { get; set; }

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

    // ADR 05 Amendments 2026-10-22: EditContext participation (issue 76).
    /// <summary>Gets or sets the expression that identifies the bound value in a cascaded <see cref="EditContext"/>.</summary>
    /// <value>
    /// The expression <c>@bind-Value</c> supplies, or <see langword="null"/>. Inside an <see cref="EditContext"/> the group
    /// then notifies the context of every change, names a field that has no name with the identifier's field name, and is
    /// invalid while the context holds messages for it. Without an <see cref="EditContext"/> it does nothing.
    /// </value>
    [Parameter]
    public Expression<Func<IReadOnlyList<string>>>? ValueExpression { get; set; }

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

        // Design point G2: the registration id without a consumer id, drawn in render order and never rendered (CKG-API-06).
        registrationId = IdGenerator.Next();
        context = new CheckboxGroupContext(CurrentValue, RequestAsync, OnBoxRendered);
        participation = new EditContextParticipation(OnEditContextMessages);
        fieldScope = FieldContext;
        if (fieldScope is not null)
        {
            scope = FieldItemContext.LabelableOf(fieldScope, ItemContext);
            context.Scope = scope;
            fieldChanged = OnFieldChanged;
            getValue = GetFormValue;
            fieldScope.AddListener(fieldChanged);
        }
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        participation!.Update(CascadedEditContext, ValueExpression);
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

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        ReactToValue();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            disposed = true;
            participation?.Dispose();
            if (fieldScope is not null)
            {
                fieldScope.RemoveListener(fieldChanged!);
                using (fieldScope.Batch())
                {
                    Unmount();
                }
            }
        }

        base.Dispose(disposing);
    }

    // CKG-ARIA-01: the consumer's role wins at the merge; CKG-ARIA-02: so does the consumer's aria-labelledby.
    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        attributes.Set("role", "group");
        renderedLabelId = scope?.Label.Id;
        if (renderedLabelId is not null)
        {
            attributes.Set("aria-labelledby", renderedLabelId);
        }
    }

    // CKG-ARIA-04: the consumer's ids, then the field scope's description and error ids.
    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes)
    {
        if (scope is null)
        {
            return;
        }

        renderedDescribedBy = scope.DescribedBy(null);
        string? consumer = attributes.TryGetValue("aria-describedby", out AttributeValue current) && current.TryGetValue(out string text) ? text : null;
        if (scope.DescribedBy(consumer) is { } describedBy)
        {
            attributes.Set("aria-describedby", describedBy);
        }
        else
        {
            attributes.Remove("aria-describedby");
        }
    }

    // CHK-EVT-09, CKG-EVT-01..05: the group's half of a box's request, with the box's details.
    private async Task<bool> RequestAsync(IReadOnlyList<string> next, ChangeEventDetails details)
    {
        if (!await valueState!.NotifyAsync(next, details, OnValueChange))
        {
            return false;
        }

        await valueState.CommitAsync(next, ValueChanged);

        // The boxes whose state changed render as they are notified, and the first to finish its render runs the value
        // reaction (BoxRendered); rendering the group instead would render every box again (design point G1).
        context!.SetState(CurrentValue, AllValues, EffectiveDisabled);
        if (UpdateState())
        {
            StateHasChanged();
        }

        return true;
    }

    // A box's render after a commit: the value reaction, once per new value.
    private void OnBoxRendered()
    {
        if (!ReferenceEquals(observedValue, CurrentValue))
        {
            ReactToValue();
        }
    }

    // After a render, and after a commit the boxes have rendered: the claim, the registration and the value reaction.
    private void ReactToValue()
    {
        if (fieldScope is null)
        {
            ObserveValue();
            return;
        }

        using (fieldScope.Batch())
        {
            if (Enabled)
            {
                ClaimControlId();
                Register();
                ObserveValue();
            }
            else
            {
                Unmount();
            }
        }
    }

    // CKG-ARIA-03, FFI-ALG-07: post-render, so prerendered markup keeps the label's `for` until the claim.
    private void ClaimControlId()
    {
        if (!claimRegistered)
        {
            claimRegistered = true;
            scope!.RegisterControlId(this, null);
        }
    }

    // CKG-FRM-01/02; checkbox-group/CheckboxGroup.tsx:120 (useRegisterFieldControl). The handle is the representative
    // member's root (CKG-FOC-02), read when the registration is refreshed.
    private void Register()
    {
        if (!ShouldRegister)
        {
            Unregister();
            return;
        }

        FieldControlRegistration next = new(
            IdParameter ?? registrationId,
            participation!.FieldName,
            CurrentValue,
            getValue,
            fieldScope!.Validation.Representative()?.ControlId,
            context!.RefreshMembersAsync);
        if (registered && next == sentRegistration)
        {
            return;
        }

        registered = true;
        sentRegistration = next;
        fieldScope.RegisterControl(this, next);
    }

    private void Unregister()
    {
        if (registered)
        {
            registered = false;
            sentRegistration = null;
            fieldScope!.RegisterControl(this, null);
        }
    }

    private void Unmount()
    {
        Unregister();
        if (claimRegistered)
        {
            claimRegistered = false;
            scope!.UnregisterControlId(this);
        }
    }

    // CKG-FRM-03: outside a form the raw value.
    private object? GetFormValue() => fieldScope!.Form.FormToken is null ? CurrentValue : context!.Project(CurrentValue);

    // CKG-STA-11/12, CKG-FRM-07/08/10; checkbox-group/CheckboxGroup.tsx:122-141. Filled on mount and on every change, the
    // rest on every change; a change is a new list (ADR 08 Amendments 2026-10-22: equality by reference).
    private void ObserveValue()
    {
        IReadOnlyList<string> value = CurrentValue;
        if (observedValue is null)
        {
            observedValue = value;
            fieldScope?.SetFilled(value.Count > 0);
            return;
        }

        if (ReferenceEquals(observedValue, value))
        {
            return;
        }

        observedValue = value;
        if (fieldScope is null)
        {
            participation!.NotifyChanged();
            return;
        }

        fieldScope.SetFilled(value.Count > 0);
        fieldScope.ClearErrors();
        fieldScope.SetDirty(!SameEntries(value, fieldScope.ValidityData.InitialValue as IReadOnlyList<string> ?? s_empty));
        participation!.NotifyChanged();
        fieldScope.Observe(ChangeAfterRefreshAsync(value));
    }

    private async Task ChangeAfterRefreshAsync(IReadOnlyList<string> value)
    {
        await context!.RefreshMembersAsync().ConfigureAwait(true);
        if (!disposed && ReferenceEquals(observedValue, value))
        {
            fieldScope!.Validation.Change(value);
        }
    }

    // CKG-STA-12: areArraysEqual, element-wise.
    private static bool SameEntries(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (int i = 0; i < left.Count; i++)
        {
            if (!string.Equals(left[i], right[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    // ADR 05 Amendments 2026-10-22 item 3.
    private void OnEditContextMessages(IReadOnlyList<string> messages) => fieldScope?.SetExternalErrors(participation!, messages);

    // A change of the field's state, name, label, descriptions or disabled state; a render re-runs the registration.
    private void OnFieldChanged()
    {
        context!.SetState(CurrentValue, AllValues, EffectiveDisabled);
        if (UpdateState()
            || registered != ShouldRegister
            || !string.Equals(scope!.Label.Id, renderedLabelId, StringComparison.Ordinal)
            || !string.Equals(scope.DescribedBy(null), renderedDescribedBy, StringComparison.Ordinal))
        {
            StateHasChanged();
        }
    }

    // Returns whether the state changed; a new record is made only then (ADR 16 rule 7). CKG-STA-01: the field's state with
    // the group's disabled state; outside a field, the default state.
    private bool UpdateState()
    {
        FieldStateSnapshot snapshot = (fieldScope?.State ?? FieldStateSnapshot.Default) with { Disabled = EffectiveDisabled };
        if (state is { } current
            && current.Disabled == snapshot.Disabled
            && current.Touched == snapshot.Touched
            && current.Dirty == snapshot.Dirty
            && current.Valid == snapshot.Valid
            && current.Filled == snapshot.Filled
            && current.Focused == snapshot.Focused)
        {
            return false;
        }

        state = new CheckboxGroupState(snapshot.Disabled, snapshot.Touched, snapshot.Dirty, snapshot.Valid, snapshot.Filled, snapshot.Focused);
        return true;
    }
}
