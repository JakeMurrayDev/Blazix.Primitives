using Blazix.Primitives.Utilities;
using Microsoft.AspNetCore.Components.Forms;

namespace Blazix.Primitives;

// The root's Field and Form half (AriaCombobox.tsx:146-160, 584-591, 1243-1323; CMB §8; FFI-ALG-06, FFI-ALG-12,
// FFI-STA-04/05, FFI-FRM-26; ADR 05 Amendments 2026-10-22 for the EditContext). After each render of the root: its explicit
// id claims the field's control id (an id-less root resets the claim, so its id is the field's), the active-control
// registration (the raw value, the form value, the element a blocked submission focuses: the input outside the popup, else
// the trigger), and the filled state (the input text without a selection, else a selected value). On a selection change
// (the input text's in no-selection mode): the form's errors for the name cleared, dirty against the initial value, the
// EditContext notified and the change validated. The input or the trigger calls Blur on focus loss: touched, and a commit
// in onBlur mode.
/// <summary>The field and form participation of a combobox-family root.</summary>
/// <typeparam name="TValue">The type of the values.</typeparam>
internal sealed class ComboboxFieldBehavior<TValue> : IDisposable
{
    private readonly ComboboxStore<TValue> store;

    private readonly Func<string> serialize;

    private readonly EditContextParticipation participation;

    private readonly Func<object?> getValue;

    private FieldRootContext? field;

    private IFormScope? form;

    private FieldControlRegistration? sent;

    private string? claimedId;

    private bool claimed;

    private bool observed;

    private int observedSelection;

    private string observedInput = string.Empty;

    private int boxedFor = -1;

    private object? boxed;

    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="ComboboxFieldBehavior{TValue}"/> class.</summary>
    /// <param name="store">The root's store.</param>
    /// <param name="serialize">Returns the value the hidden input submits.</param>
    public ComboboxFieldBehavior(ComboboxStore<TValue> store, Func<string> serialize)
    {
        this.store = store;
        this.serialize = serialize;
        participation = new EditContextParticipation(OnEditContextMessages);
        getValue = () => this.serialize();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        participation.Dispose();
        if (field is not null)
        {
            using (field.Batch())
            {
                if (sent is not null)
                {
                    field.RegisterControl(this, null);
                }

                if (claimed)
                {
                    field.Labelable.UnregisterControlId(this);
                }

                field.SetExternalErrors(participation, null);
            }
        }
    }

    /// <summary>Gets the name the form value is submitted under: the field's, else the root's, else the EditContext field's.</summary>
    /// <param name="own">The root's <c>Name</c>.</param>
    /// <returns>The name, or <see langword="null"/>.</returns>
    public string? NameOf(string? own) => field?.Name ?? own ?? participation.FieldName;

    /// <summary>Takes the root's cascades and inputs.</summary>
    /// <param name="fieldContext">The enclosing field, or <see langword="null"/>.</param>
    /// <param name="formScope">The enclosing form, or <see langword="null"/>.</param>
    /// <param name="editContext">The enclosing edit context, or <see langword="null"/>.</param>
    /// <param name="inputs">The root's inputs.</param>
    public void Apply(FieldRootContext? fieldContext, IFormScope? formScope, EditContext? editContext, ComboboxRootInputs<TValue> inputs)
    {
        field = fieldContext;
        form = formScope;
        switch (inputs.SelectionMode)
        {
            case ComboboxSelectionMode.Multiple:
                participation.Update(editContext, inputs.ValuesExpression);
                break;
            case ComboboxSelectionMode.None:
                participation.Update(editContext, inputs.InputValueExpression);
                break;
            default:
                participation.Update(editContext, inputs.ValueExpression);
                break;
        }
    }

    /// <summary>Registers the control, claims the control id, publishes the filled state and reacts to a changed value.</summary>
    /// <param name="explicitId">The root's own <c>Id</c>, or <see langword="null"/>.</param>
    /// <param name="name">The root's <c>Name</c>, or <see langword="null"/>.</param>
    public void AfterRender(string? explicitId, string? name)
    {
        if (disposed)
        {
            return;
        }

        if (field is null)
        {
            ObserveValue(name);
            return;
        }

        using (field.Batch())
        {
            ClaimControlId(explicitId);
            Register(name);
            field.SetFilled(Filled());
            ObserveValue(name);
        }
    }

    /// <summary>Records a focus loss of the input outside the popup or of the trigger: touched, and a commit in onBlur mode.</summary>
    public void Blur()
    {
        if (disposed || field is null)
        {
            return;
        }

        field.SetTouched(true);
        if (field.ValidationMode == ValidationMode.OnBlur)
        {
            field.Observe(field.Validation.CommitAsync(RawValue()));
        }
    }

    // useLabelableId({ id: idProp }) (AriaCombobox.tsx:160): an explicit id claims the control id; an id-less root resets the
    // claim, so the field's own control id stays the root's.
    private void ClaimControlId(string? explicitId)
    {
        LabelableScope scope = field!.Labelable;
        if (explicitId is not null)
        {
            if (!claimed || !string.Equals(claimedId, explicitId, StringComparison.Ordinal))
            {
                claimed = true;
                claimedId = explicitId;
                scope.RegisterControlId(this, explicitId);
            }

            return;
        }

        if (claimed)
        {
            claimed = false;
            claimedId = null;
            scope.UnregisterControlId(this);
        }

        scope.ResetControlId();
    }

    // useRegisterFieldControl (AriaCombobox.tsx:584-591): enabled while not disabled.
    private void Register(string? name)
    {
        ComboboxStoreState state = store.State;
        if (state.Disabled)
        {
            if (sent is not null)
            {
                sent = null;
                field!.RegisterControl(this, null);
            }

            return;
        }

        string? controlId = state.InputInsidePopup ? state.ElementIds.Trigger : state.ElementIds.Input;
        object? value = RawValue();
        FieldControlRegistration next = new(state.Id, name, value, getValue, controlId, null);
        if (sent is not null && next == sent && ReferenceEquals(value, sent.Value))
        {
            return;
        }

        sent = next;
        field!.RegisterControl(this, next);
    }

    // AriaCombobox.tsx:1243-1252.
    private bool Filled() => store.State.SelectionMode == ComboboxSelectionMode.None ? store.State.InputValue.Length != 0 : store.State.HasSelectedValue;

    // AriaCombobox.tsx:1294-1323 (useValueChanged): nothing on mount; on a change, the errors cleared, dirty, the EditContext
    // notified and the change validated.
    private void ObserveValue(string? name)
    {
        ComboboxStoreState state = store.State;
        bool none = state.SelectionMode == ComboboxSelectionMode.None;
        if (!observed)
        {
            observed = true;
            observedSelection = state.SelectionRevision;
            observedInput = state.InputValue;
            return;
        }

        if (none ? string.Equals(observedInput, state.InputValue, StringComparison.Ordinal) : observedSelection == state.SelectionRevision)
        {
            return;
        }

        observedSelection = state.SelectionRevision;
        observedInput = state.InputValue;
        object? value = RawValue();
        if (field is null)
        {
            form?.ClearErrors(NameOf(name));
            participation.NotifyChanged();
            return;
        }

        field.ClearErrors();
        field.SetDirty(IsDirty(none));
        participation.NotifyChanged();
        field.Validation.Change(value);
    }

    // isSelectedValueDirty (AriaCombobox.tsx:1297) and `inputValue !== initialValue` (:1321).
    private bool IsDirty(bool none)
    {
        object? initial = field!.ValidityData.InitialValue;
        if (none)
        {
            return !string.Equals(initial as string, store.State.InputValue, StringComparison.Ordinal);
        }

        Func<TValue, TValue, bool>? comparer = store.Inputs.IsItemEqualToValue;
        if (store.State.SelectionMode == ComboboxSelectionMode.Multiple)
        {
            IReadOnlyList<TValue> values = store.SelectedValues;
            if (initial is not IReadOnlyList<TValue> before || before.Count != values.Count)
            {
                return initial is not null || values.Count != 0;
            }

            for (int i = 0; i < values.Count; i++)
            {
                if (!ItemEquality.Compare(before[i], values[i], comparer))
                {
                    return true;
                }
            }

            return false;
        }

        if (!store.State.HasSelectedValue)
        {
            return initial is not null;
        }

        return initial is not TValue captured || !ItemEquality.Compare(captured, store.SelectedValue, comparer);
    }

    // The registration and the validator take the value as an object; boxed once per committed selection, so a render boxes
    // nothing.
    private object? RawValue()
    {
        ComboboxStoreState state = store.State;
        if (state.SelectionMode == ComboboxSelectionMode.None)
        {
            return state.InputValue;
        }

        if (boxedFor != state.SelectionRevision)
        {
            boxedFor = state.SelectionRevision;
            boxed = state.SelectionMode == ComboboxSelectionMode.Multiple ? store.SelectedValues : state.HasSelectedValue ? store.SelectedValue : null;
        }

        return boxed;
    }

    private void OnEditContextMessages(IReadOnlyList<string> messages) => field?.SetExternalErrors(participation, messages);
}
