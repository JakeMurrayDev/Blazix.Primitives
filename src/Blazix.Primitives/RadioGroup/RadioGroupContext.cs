using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// RDG §11 (the RadioGroupContext a Radio.Root reads); radio-group/RadioGroupContext.ts. Design point R3 (pending the user):
// a plain listener list cascaded fixed (ADR 16 rule 9), the Switch, Checkbox and CheckboxGroup pattern. A change of the
// value, the flags, the name or the form notifies every radio, which compares what it renders and renders only when that
// changed, so a selection renders the radios whose Checked flipped and not the group (rules 8 and 9). The radios also
// register here in render order: the group refreshes their hidden inputs, projects its form value onto them, clears the
// field's focused flag their focus set, restores them after a refused selection and finds one by its element id.
/// <summary>The state a radio group shares with the radio buttons in its subtree.</summary>
/// <typeparam name="TValue">The type of the group's value.</typeparam>
internal sealed class RadioGroupContext<TValue>
{
    private readonly List<Action> listeners = [];
    private readonly List<RadioRoot<TValue>> radios = [];

    /// <summary>Initializes a new instance of the <see cref="RadioGroupContext{TValue}"/> class.</summary>
    /// <param name="request">Runs the group's half of a radio's request: its change callback, then its commit.</param>
    /// <param name="radioRendered">Runs the group's work that follows a radio's render.</param>
    public RadioGroupContext(Func<TValue, ChangeEventDetails, Task<bool>> request, Action radioRendered)
    {
        Request = request;
        RadioRendered = radioRendered;
    }

    /// <summary>Gets the group's value.</summary>
    /// <value>The committed value; meaningful only while <see cref="HasSelection"/> is <see langword="true"/>.</value>
    public TValue Value { get; private set; } = default!;

    /// <summary>Gets a value indicating whether the group holds a value at all.</summary>
    /// <value>
    /// <see langword="false"/> while an uncontrolled group without a default value has not been selected (design point R1,
    /// pending the user); a <see langword="null"/> value also selects no radio.
    /// </value>
    public bool HasSelection { get; private set; }

    /// <summary>Gets a value indicating whether every radio of the group is disabled.</summary>
    /// <value>The group's effective disabled state.</value>
    public bool Disabled { get; private set; }

    /// <summary>Gets a value indicating whether no radio of the group can be selected by the user.</summary>
    /// <value>The group's read-only state.</value>
    public bool ReadOnly { get; private set; }

    /// <summary>Gets a value indicating whether a radio of the group must be selected for the form to submit.</summary>
    /// <value>The group's required state.</value>
    public bool Required { get; private set; }

    /// <summary>Gets the name every radio's hidden input carries.</summary>
    /// <value>The group's effective name, or <see langword="null"/>.</value>
    public string? Name { get; private set; }

    /// <summary>Gets the id of the form every radio's hidden input belongs to.</summary>
    /// <value>The group's form id, or <see langword="null"/>.</value>
    public string? Form { get; private set; }

    // Design point R6 (pending the user), CheckboxGroup's precedent (CKG-ARIA-07): a radio that shares its group's labelable
    // scope claims no control id, so the radios' hidden inputs keep distinct ids and the form's submit state reaches each.
    /// <summary>Gets or sets the labelable scope the group renders in.</summary>
    /// <value>The scope, or <see langword="null"/> outside a field; a radio that shares it claims no control id.</value>
    public LabelableScope? Scope { get; set; }

    /// <summary>Gets the group's half of a radio's request.</summary>
    /// <value>A function that receives the radio's value and details and returns whether the request was not canceled.</value>
    public Func<TValue, ChangeEventDetails, Task<bool>> Request { get; }

    // A commit renders the radios whose state changed, not the group, so the group's value reaction runs after the first of
    // those renders, where the group's own render would have run it (CheckboxGroup's design point G1).
    /// <summary>Gets the group's work that follows a radio's render, which a radio runs after each of its renders.</summary>
    /// <value>An action that runs the value reaction when the value changed since it last ran.</value>
    public Action RadioRendered { get; }

    // RAD-STA-01 with ADR 08 Amendments 2026-10-22: `null` selects nothing, so a null group value checks no radio, a null
    // radio value included (radio.md's dated note on `null === null`).
    /// <summary>Determines whether a radio with the given value is selected.</summary>
    /// <param name="value">The radio's value.</param>
    /// <returns><see langword="true"/> when the group holds a value that is not <see langword="null"/> and strictly equals <paramref name="value"/>.</returns>
    public bool IsSelected(TValue value) => HasSelection && !RadioValues.IsNull(Value) && Equality.StrictEquals(Value, value);

    /// <summary>Takes the group's state and notifies the radios when it changed.</summary>
    /// <param name="value">The value.</param>
    /// <param name="hasSelection">Whether the group holds a value.</param>
    /// <param name="disabled">The group's effective disabled state.</param>
    /// <param name="readOnly">The group's read-only state.</param>
    /// <param name="required">The group's required state.</param>
    /// <param name="name">The group's effective name.</param>
    /// <param name="form">The group's form id.</param>
    public void SetState(TValue value, bool hasSelection, bool disabled, bool readOnly, bool required, string? name, string? form)
    {
        if (hasSelection == HasSelection
            && Equality.Is(value, Value)
            && disabled == Disabled
            && readOnly == ReadOnly
            && required == Required
            && string.Equals(name, Name, StringComparison.Ordinal)
            && string.Equals(form, Form, StringComparison.Ordinal))
        {
            return;
        }

        Value = value;
        HasSelection = hasSelection;
        Disabled = disabled;
        ReadOnly = readOnly;
        Required = required;
        Name = name;
        Form = form;
        Notify();
    }

    /// <summary>Adds a listener that runs after every change of the group's state.</summary>
    /// <param name="listener">The listener.</param>
    public void AddListener(Action listener) => listeners.Add(listener);

    /// <summary>Removes a listener added with <see cref="AddListener"/>.</summary>
    /// <param name="listener">The listener.</param>
    public void RemoveListener(Action listener) => listeners.Remove(listener);

    /// <summary>Adds a radio of the group, in render order.</summary>
    /// <param name="radio">The radio.</param>
    public void Register(RadioRoot<TValue> radio) => radios.Add(radio);

    /// <summary>Removes a radio added with <see cref="Register"/>.</summary>
    /// <param name="radio">The radio.</param>
    public void Unregister(RadioRoot<TValue> radio) => radios.Remove(radio);

    /// <summary>Finds the radio whose element has the given id.</summary>
    /// <param name="id">The element id, or <see langword="null"/>.</param>
    /// <returns>The radio, or <see langword="null"/>.</returns>
    public RadioRoot<TValue>? Find(string? id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        for (int i = 0; i < radios.Count; i++)
        {
            if (string.Equals(radios[i].ElementId, id, StringComparison.Ordinal))
            {
                return radios[i];
            }
        }

        return null;
    }

    // DP6 for a group: a commit reads every radio input's native state from a fresh report (RDG-FRM-07..10).
    /// <summary>Brings the native state of every radio's hidden input up to date.</summary>
    /// <returns>A task that completes when every radio's input has reported.</returns>
    public Task RefreshMembersAsync()
    {
        if (radios.Count == 0)
        {
            return Task.CompletedTask;
        }

        if (radios.Count == 1)
        {
            return radios[0].FieldBehavior.RefreshInputAsync();
        }

        Task[] refreshes = new Task[radios.Count];
        for (int i = 0; i < radios.Count; i++)
        {
            refreshes[i] = radios[i].FieldBehavior.RefreshInputAsync();
        }

        return AwaitInTurnAsync(refreshes);
    }

    // RDG-FRM-06; radio-group/RadioGroup.tsx:154-167 (getFormValue inside a form): some radio's input is checked and eligible.
    /// <summary>Determines whether a radio of the group is checked and would be submitted with its form.</summary>
    /// <returns><see langword="true"/> when a selected radio's hidden input is eligible.</returns>
    public bool AnySubmitted()
    {
        for (int i = 0; i < radios.Count; i++)
        {
            CheckableFieldBehavior field = radios[i].FieldBehavior;
            if (radios[i].Checked && field.ValidationInput.State.Eligible)
            {
                return true;
            }
        }

        return false;
    }

    // RDG-FOC-04/05: the group's leaving blur clears the field's focused flag whichever radio's focus set it, as Base UI's
    // raw setFocused(false) does.
    /// <summary>Releases the field's focused flag that a radio of the group acquired.</summary>
    /// <param name="field">The group's field.</param>
    public void ReleaseFocused(FieldRootContext field)
    {
        for (int i = 0; i < radios.Count; i++)
        {
            field.SetFocused(radios[i].FieldBehavior, false, false);
        }
    }

    // RDG-EVT-04 on Server (design point R2 (c), pending the user): the browser can only have unchecked a radio that was
    // checked, which is one showing the current value.
    /// <summary>Restores the hidden inputs of the radios that show the group's value, after another radio's request was refused.</summary>
    /// <param name="refused">The radio whose request was refused, which restores its own input.</param>
    public void RestoreSelected(RadioRoot<TValue> refused)
    {
        for (int i = 0; i < radios.Count; i++)
        {
            if (!ReferenceEquals(radios[i], refused) && radios[i].Checked)
            {
                radios[i].RestoreCheckedInput();
            }
        }
    }

    /// <summary>Gets the fragment that cascades this context to <paramref name="content"/>.</summary>
    /// <param name="content">The content that receives the context.</param>
    /// <returns>The fragment.</returns>
    internal RenderFragment Cascade(RenderFragment? content) => builder =>
    {
        builder.OpenComponent<CascadingValue<RadioGroupContext<TValue>>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<RadioGroupContext<TValue>>.Value), this);
        builder.AddComponentParameter(2, nameof(CascadingValue<RadioGroupContext<TValue>>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<RadioGroupContext<TValue>>.ChildContent), content);
        builder.CloseComponent();
    };

    // Every refresh is requested before the first await; awaiting them in turn, rather than with Task.WhenAll, resumes on
    // the renderer's dispatcher after each report instead of on the thread pool.
    private static async Task AwaitInTurnAsync(Task[] refreshes)
    {
        foreach (Task refresh in refreshes)
        {
            await refresh.ConfigureAwait(true);
        }
    }

    private void Notify()
    {
        // A listener may remove itself or another (a radio disposed by the render it triggers): iterating backwards with a
        // bounds check never skips a listener that is still registered.
        for (int i = listeners.Count - 1; i >= 0; i--)
        {
            if (i < listeners.Count)
            {
                listeners[i]();
            }
        }
    }
}

