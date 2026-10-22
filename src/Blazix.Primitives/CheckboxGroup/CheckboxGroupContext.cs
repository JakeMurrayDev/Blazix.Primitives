using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// checkbox-group §11 (the CheckboxGroupContext a Checkbox.Root reads); checkbox-group/CheckboxGroupContext.ts. Design point
// G1 of M2 track 6 (pending the user): a plain listener list cascaded fixed (ADR 16 rule 9), the Switch and Checkbox
// pattern. A change of the value, AllValues, the disabled state or the parent's member ids notifies every box, which
// compares what it renders and renders only when that changed.
/// <summary>The state a checkbox group shares with the boxes in its subtree.</summary>
internal sealed class CheckboxGroupContext
{
    private readonly List<Action> listeners = [];
    private readonly List<Member> members = [];

    /// <summary>Initializes a new instance of the <see cref="CheckboxGroupContext"/> class.</summary>
    /// <param name="initialValue">The group's value at its first render.</param>
    /// <param name="request">Runs the group's half of a box's request: its change callback, then its commit.</param>
    public CheckboxGroupContext(IReadOnlyList<string> initialValue, Func<IReadOnlyList<string>, ChangeEventDetails, Task<bool>> request)
    {
        Value = initialValue;
        Request = request;
        Parent = new CheckboxGroupParent(initialValue);
        Parent.ChildIdsChanged += Notify;
    }

    /// <summary>Gets the group's value.</summary>
    /// <value>The identities of the ticked members.</value>
    public IReadOnlyList<string> Value { get; private set; }

    /// <summary>Gets the identities of all members.</summary>
    /// <value>The list, or <see langword="null"/> when the group has no parent behavior.</value>
    public IReadOnlyList<string>? AllValues { get; private set; }

    /// <summary>Gets a value indicating whether every box of the group is disabled.</summary>
    /// <value>The group's effective disabled state.</value>
    public bool Disabled { get; private set; }

    /// <summary>Gets the parent algorithm and the members' registries.</summary>
    /// <value>The group's parent state.</value>
    public CheckboxGroupParent Parent { get; }

    /// <summary>Gets the group's half of a box's request.</summary>
    /// <value>A function that receives the proposed value and the box's details and returns whether the value was committed.</value>
    public Func<IReadOnlyList<string>, ChangeEventDetails, Task<bool>> Request { get; }

    /// <summary>Gets or sets the labelable scope the group renders in.</summary>
    /// <value>The scope, or <see langword="null"/> outside a field; a box that shares it claims no control id.</value>
    public LabelableScope? Scope { get; set; }

    // CKG §8 (the validation registry keyed by identity): the members whose hidden inputs are the field's validation inputs,
    // in registration order, with the identity each registered for the form projection (CKG-FRM-03).
    /// <summary>Adds or updates a member whose hidden input is one of its field's validation inputs.</summary>
    /// <param name="behavior">The member's field behavior.</param>
    /// <param name="identity">The member's identity, or <see langword="null"/> when it has none, which the projection never lists.</param>
    public void RegisterMember(CheckableFieldBehavior behavior, string? identity)
    {
        for (int i = 0; i < members.Count; i++)
        {
            if (ReferenceEquals(members[i].Behavior, behavior))
            {
                members[i] = new Member(behavior, identity);
                return;
            }
        }

        members.Add(new Member(behavior, identity));
    }

    /// <summary>Removes a member added with <see cref="RegisterMember"/>.</summary>
    /// <param name="behavior">The member's field behavior.</param>
    public void UnregisterMember(CheckableFieldBehavior behavior)
    {
        for (int i = 0; i < members.Count; i++)
        {
            if (ReferenceEquals(members[i].Behavior, behavior))
            {
                members.RemoveAt(i);
                return;
            }
        }
    }

    // DP6 for a group: a commit reads every member input's native state from a fresh report.
    /// <summary>Brings the native state of every member's hidden input up to date.</summary>
    /// <returns>A task that completes when every member's input has reported.</returns>
    public Task RefreshMembersAsync()
    {
        if (members.Count == 0)
        {
            return Task.CompletedTask;
        }

        if (members.Count == 1)
        {
            return members[0].Behavior.RefreshInputAsync();
        }

        Task[] refreshes = new Task[members.Count];
        for (int i = 0; i < members.Count; i++)
        {
            refreshes[i] = members[i].Behavior.RefreshInputAsync();
        }

        return AwaitInTurnAsync(refreshes);
    }

    // CKG-FRM-03; checkbox-group/CheckboxGroup.tsx:100-118 (getFormValue).
    /// <summary>Projects the value onto the members whose hidden inputs are checked and eligible, in value order.</summary>
    /// <param name="value">The group's value.</param>
    /// <returns>The projection.</returns>
    public IReadOnlyList<string> Project(IReadOnlyList<string> value)
    {
        List<string> projected = [];
        foreach (string entry in value)
        {
            foreach (Member member in members)
            {
                if (string.Equals(member.Identity, entry, StringComparison.Ordinal) && member.Behavior.Checked && member.Behavior.ValidationInput.State.Eligible)
                {
                    projected.Add(entry);
                    break;
                }
            }
        }

        return projected;
    }

    /// <summary>Takes the group's state and notifies the boxes when it changed.</summary>
    /// <param name="value">The value.</param>
    /// <param name="allValues">The identities of all members, or <see langword="null"/>.</param>
    /// <param name="disabled">The group's effective disabled state.</param>
    public void SetState(IReadOnlyList<string> value, IReadOnlyList<string>? allValues, bool disabled)
    {
        if (ReferenceEquals(value, Value) && ReferenceEquals(allValues, AllValues) && disabled == Disabled)
        {
            return;
        }

        Value = value;
        AllValues = allValues;
        Disabled = disabled;
        Notify();
    }

    /// <summary>Adds a listener that runs after every change of the group's state.</summary>
    /// <param name="listener">The listener.</param>
    public void AddListener(Action listener) => listeners.Add(listener);

    /// <summary>Removes a listener added with <see cref="AddListener"/>.</summary>
    /// <param name="listener">The listener.</param>
    public void RemoveListener(Action listener) => listeners.Remove(listener);

    /// <summary>Gets the fragment that cascades this context to <paramref name="content"/>.</summary>
    /// <param name="content">The content that receives the context.</param>
    /// <returns>The fragment.</returns>
    internal RenderFragment Cascade(RenderFragment? content) => builder =>
    {
        builder.OpenComponent<CascadingValue<CheckboxGroupContext>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<CheckboxGroupContext>.Value), this);
        builder.AddComponentParameter(2, nameof(CascadingValue<CheckboxGroupContext>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<CheckboxGroupContext>.ChildContent), content);
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
        // A listener may remove itself or another (a box disposed by the render it triggers): iterating backwards with a
        // bounds check never skips a listener that is still registered.
        for (int i = listeners.Count - 1; i >= 0; i--)
        {
            if (i < listeners.Count)
            {
                listeners[i]();
            }
        }
    }

    private readonly record struct Member(CheckableFieldBehavior Behavior, string? Identity);
}
