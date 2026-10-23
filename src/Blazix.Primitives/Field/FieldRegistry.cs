using Blazix.Primitives.Interop;

namespace Blazix.Primitives;

// FFI §2 (field registry), FFI-FRM-17..21, FFI-API-07; internals/form-context/FormContext.ts:13-28 (`formRef.current.fields`,
// an insertion-ordered Map keyed by control id). Written by fields (FFI-ALG-12/14), read by the form: replacing an entry
// keeps its position (a Map's set), removing and adding moves it to the end.
/// <summary>The fields a form validates and submits, keyed by control id, in registration order.</summary>
internal sealed class FieldRegistry
{
    private readonly List<string> order = [];
    private readonly Dictionary<string, FieldRegistryEntry> entries = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FieldValidationInput> inputs = new(StringComparer.Ordinal);
    private readonly bool accepts;

    /// <summary>Initializes a new instance of the <see cref="FieldRegistry"/> class that accepts entries.</summary>
    public FieldRegistry()
        : this(accepts: true)
    {
    }

    private FieldRegistry(bool accepts)
    {
        this.accepts = accepts;
    }

    /// <summary>Gets the registry outside a form, which accepts no entry.</summary>
    /// <value>An always-empty registry.</value>
    public static FieldRegistry None { get; } = new(accepts: false);

    /// <summary>Gets the number of entries.</summary>
    /// <value>The number of registered fields.</value>
    public int Count => order.Count;

    /// <summary>Gets the control ids in registration order.</summary>
    /// <value>The keys.</value>
    public IReadOnlyList<string> Ids => order;

    /// <summary>Determines whether an entry has the given control id.</summary>
    /// <param name="id">The control id.</param>
    /// <returns><see langword="true"/> if the id is registered; otherwise, <see langword="false"/>.</returns>
    public bool Contains(string id) => entries.ContainsKey(id);

    /// <summary>Gets the entry of a control id.</summary>
    /// <param name="id">The control id.</param>
    /// <param name="entry">The entry when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> if the id is registered; otherwise, <see langword="false"/>.</returns>
    public bool TryGet(string id, out FieldRegistryEntry entry) => entries.TryGetValue(id, out entry!);

    /// <summary>Adds or replaces the entry of a control id; a replaced entry keeps its position.</summary>
    /// <param name="id">The control id.</param>
    /// <param name="entry">The entry.</param>
    public void Set(string id, FieldRegistryEntry entry)
    {
        if (!accepts)
        {
            return;
        }

        if (!entries.ContainsKey(id))
        {
            order.Add(id);
        }

        entries[id] = entry;
    }

    /// <summary>Replaces the validity of a registered entry.</summary>
    /// <param name="id">The control id.</param>
    /// <param name="validityData">The validity, with the external invalid state folded in.</param>
    public void SetValidity(string id, FieldValidityData validityData)
    {
        if (entries.TryGetValue(id, out FieldRegistryEntry? entry))
        {
            entries[id] = entry with { ValidityData = validityData };
        }
    }

    /// <summary>Removes the entry of a control id.</summary>
    /// <param name="id">The control id.</param>
    /// <returns><see langword="true"/> if an entry was removed; otherwise, <see langword="false"/>.</returns>
    public bool Remove(string id)
    {
        if (!entries.Remove(id))
        {
            return false;
        }

        order.Remove(id);
        return true;
    }

    // FFI-FRM-04, FFI-FRM-24 (M2 track 6, settled by the orchestrator 2026-10-23 (F2/DP6, ADR 16 rule 6)): the hidden inputs of checkbox and radio controls are
    // validation inputs whose element is not the field's entry (a box's entry is keyed by its root, a group's has no
    // element), so the state the module reads from them when the form submits reaches them by their element id.
    /// <summary>Registers a validation input under the id of its element, for the state read when the form submits.</summary>
    /// <param name="id">The element's id.</param>
    /// <param name="input">The validation input.</param>
    public void SetInput(string id, FieldValidationInput input)
    {
        if (accepts)
        {
            inputs[id] = input;
        }
    }

    /// <summary>Removes a validation input added with <see cref="SetInput"/>, unless another input took its id since.</summary>
    /// <param name="id">The element's id.</param>
    /// <param name="input">The validation input.</param>
    public void RemoveInput(string id, FieldValidationInput input)
    {
        if (inputs.TryGetValue(id, out FieldValidationInput? current) && ReferenceEquals(current, input))
        {
            inputs.Remove(id);
        }
    }

    /// <summary>Gets the validation input registered under an element id.</summary>
    /// <param name="id">The element's id.</param>
    /// <param name="input">The input when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> if an input is registered under the id; otherwise, <see langword="false"/>.</returns>
    public bool TryGetInput(string id, out FieldValidationInput input) => inputs.TryGetValue(id, out input!);

    // FFI-API-07, FFI-EVT-05: computed at call time, named entries only, registration order.
    /// <summary>Builds the values of the named entries.</summary>
    /// <returns>The values, or <see cref="FormValues.Empty"/> when no entry has a name.</returns>
    public FormValues BuildValues()
    {
        if (order.Count == 0)
        {
            return FormValues.Empty;
        }

        KeyValuePair<string?, object?>[] pairs = new KeyValuePair<string?, object?>[order.Count];
        for (int i = 0; i < order.Count; i++)
        {
            FieldRegistryEntry entry = entries[order[i]];
            pairs[i] = new KeyValuePair<string?, object?>(entry.Name, string.IsNullOrEmpty(entry.Name) ? null : entry.GetValue());
        }

        return FormValues.Create(pairs);
    }
}

// FFI §2 (registry entry), FFI-ALG-12 `refresh()`; internals/form-context/FormContext.ts:15-27.
/// <summary>One registered field: its name, its value projection, its validity, its imperative validation and its control.</summary>
/// <param name="Name">The field's effective name, or <see langword="null"/> for a field that submits nothing.</param>
/// <param name="GetValue">Returns the control's value as the form's values carry it.</param>
/// <param name="ValidityData">The field's validity with the external invalid state folded in.</param>
/// <param name="Validate">Runs the field's imperative validation; its synchronous part has updated this entry when it returns.</param>
/// <param name="ControlId">The id of the element focused when the form blocks a submission, or <see langword="null"/>.</param>
/// <param name="ApplyState">Takes the control's value and native validity read when the form submitted, or <see langword="null"/>.</param>
/// <param name="ResolveControlId">Returns the id of the element the form focuses, read when it blocks a submission, or <see langword="null"/> to use <paramref name="ControlId"/>.</param>
internal sealed record FieldRegistryEntry(string? Name, Func<object?> GetValue, FieldValidityData ValidityData, Func<Task> Validate, string? ControlId, Action<string?, NativeValidity>? ApplyState = null, Func<string?>? ResolveControlId = null);
