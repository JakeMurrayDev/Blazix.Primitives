namespace Blazix.Primitives;

// checkbox-group §4 (the parent toggle algorithm) and §8's registries; checkbox-group/useCheckboxGroupParent.ts. Pure C#
// with no Blazor reference (L1). Identities are opaque strings in ordinal dictionaries (CKG-EDGE-04).
// - The snapshot is the first-render value, replaced by every committed member toggle (CKG-STA-08); it is not re-synced to
//   a controlled value the consumer changes (§16 Q6, mirrored).
// - The cycle status advances only when the parent's proposal was not canceled (CKG-STA-07).
// - The parent derives its state from lengths, not membership (CKG-STA-06, CKG-EDGE-10, §16 Q2).
// - Removing an identity that is absent changes nothing (§16 Q5, the port's guard).
/// <summary>The state behind a checkbox group's parent box: the snapshot, the cycle, and the members' ids and disabled states.</summary>
internal sealed class CheckboxGroupParent
{
    private readonly Dictionary<string, bool> disabledStates = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> childIds = new(StringComparer.Ordinal);
    private IReadOnlyList<string> snapshot;
    private ParentCycle status = ParentCycle.Mixed;

    /// <summary>Occurs when the registered member ids change.</summary>
    public event Action? ChildIdsChanged;

    /// <summary>Initializes a new instance of the <see cref="CheckboxGroupParent"/> class.</summary>
    /// <param name="initialValue">The group's value at its first render, the first snapshot.</param>
    public CheckboxGroupParent(IReadOnlyList<string> initialValue)
    {
        snapshot = initialValue;
    }

    /// <summary>Determines whether the parent renders checked.</summary>
    /// <param name="value">The group's value.</param>
    /// <param name="allValues">The group's identities of all members.</param>
    /// <returns><see langword="true"/> when the value has as many entries as <paramref name="allValues"/>.</returns>
    public static bool IsChecked(IReadOnlyList<string> value, IReadOnlyList<string> allValues) => value.Count == allValues.Count;

    /// <summary>Determines whether the parent renders mixed.</summary>
    /// <param name="value">The group's value.</param>
    /// <param name="allValues">The group's identities of all members.</param>
    /// <returns><see langword="true"/> when the value is not empty and its length differs from <paramref name="allValues"/>'.</returns>
    public static bool IsIndeterminate(IReadOnlyList<string> value, IReadOnlyList<string> allValues) => value.Count != allValues.Count && value.Count > 0;

    // CKG-STA-03: append when checking, remove when unchecking, the order of the others kept; always a new list.
    /// <summary>Proposes the value of one member's toggle.</summary>
    /// <param name="value">The group's value.</param>
    /// <param name="identity">The member's identity.</param>
    /// <param name="check">Whether the member is being checked.</param>
    /// <returns>A new list.</returns>
    public static IReadOnlyList<string> ProposeMemberToggle(IReadOnlyList<string> value, string identity, bool check)
    {
        if (check)
        {
            string[] appended = new string[value.Count + 1];
            for (int i = 0; i < value.Count; i++)
            {
                appended[i] = value[i];
            }

            appended[value.Count] = identity;
            return appended;
        }

        int index = -1;
        for (int i = 0; i < value.Count; i++)
        {
            if (string.Equals(value[i], identity, StringComparison.Ordinal))
            {
                index = i;
                break;
            }
        }

        string[] remaining = new string[index < 0 ? value.Count : value.Count - 1];
        for (int i = 0, j = 0; i < value.Count; i++)
        {
            if (i != index)
            {
                remaining[j++] = value[i];
            }
        }

        return remaining;
    }

    // CKG-STA-08: a committed member toggle replaces the snapshot and resets the cycle.
    /// <summary>Records a committed member toggle.</summary>
    /// <param name="committed">The committed value.</param>
    public void CommitMemberToggle(IReadOnlyList<string> committed)
    {
        snapshot = committed;
        status = ParentCycle.Mixed;
    }

    // CKG-STA-07; useCheckboxGroupParent.ts:54-96.
    /// <summary>Proposes the value of a toggle of the parent.</summary>
    /// <param name="value">The group's value.</param>
    /// <param name="allValues">The group's identities of all members.</param>
    /// <returns>The proposed value and the cycle status to move to when it is not canceled.</returns>
    public ParentProposal ProposeParentToggle(IReadOnlyList<string> value, IReadOnlyList<string> allValues)
    {
        // None except the disabled ones that are checked; all except the disabled ones that are unchecked.
        List<string> none = [];
        List<string> all = [];
        foreach (string identity in allValues)
        {
            bool disabled = disabledStates.TryGetValue(identity, out bool state) && state;
            bool inSnapshot = Contains(snapshot, identity);
            if (disabled && inSnapshot)
            {
                none.Add(identity);
            }

            if (!disabled || inSnapshot)
            {
                all.Add(identity);
            }
        }

        if (snapshot.Count == all.Count || snapshot.Count == 0)
        {
            return new ParentProposal(value.Count == all.Count ? none : all, null);
        }

        return status switch
        {
            ParentCycle.Mixed => new ParentProposal(all, ParentCycle.On),
            ParentCycle.On => new ParentProposal(none, ParentCycle.Off),
            _ => new ParentProposal(snapshot, ParentCycle.Mixed),
        };
    }

    /// <summary>Records a parent toggle that was not canceled.</summary>
    /// <param name="proposal">The proposal that was committed.</param>
    public void CommitParentToggle(ParentProposal proposal)
    {
        if (proposal.NextStatus is { } next)
        {
            status = next;
        }
    }

    // CKG-STA-09: the latest reported state of each identity.
    /// <summary>Records a member's effective disabled state.</summary>
    /// <param name="identity">The member's identity.</param>
    /// <param name="disabled">Whether the member is disabled.</param>
    public void SetDisabled(string identity, bool disabled) => disabledStates[identity] = disabled;

    /// <summary>Withdraws a member's disabled state.</summary>
    /// <param name="identity">The member's identity.</param>
    public void RemoveDisabled(string identity) => disabledStates.Remove(identity);

    // CKG-ARIA-06, CHK-ARIA-12: the ids of the members sharing an identity, in registration order.
    /// <summary>Registers the rendered root id of a member.</summary>
    /// <param name="identity">The member's identity.</param>
    /// <param name="id">The id on the member's root element.</param>
    public void RegisterChild(string identity, string id)
    {
        if (!childIds.TryGetValue(identity, out List<string>? ids))
        {
            ids = [];
            childIds[identity] = ids;
        }

        ids.Add(id);
        ChildIdsChanged?.Invoke();
    }

    /// <summary>Withdraws a member's id registered with <see cref="RegisterChild"/>.</summary>
    /// <param name="identity">The member's identity.</param>
    /// <param name="id">The id.</param>
    public void UnregisterChild(string identity, string id)
    {
        if (!childIds.TryGetValue(identity, out List<string>? ids) || !ids.Remove(id))
        {
            return;
        }

        if (ids.Count == 0)
        {
            childIds.Remove(identity);
        }

        ChildIdsChanged?.Invoke();
    }

    /// <summary>Composes the parent's <c>aria-controls</c>.</summary>
    /// <param name="allValues">The group's identities of all members, which give the order.</param>
    /// <returns>The space-separated ids, or <see langword="null"/> while none is registered.</returns>
    public string? AriaControls(IReadOnlyList<string> allValues)
    {
        if (childIds.Count == 0)
        {
            return null;
        }

        List<string> ids = [];
        foreach (string identity in allValues)
        {
            if (childIds.TryGetValue(identity, out List<string>? registered))
            {
                ids.AddRange(registered);
            }
        }

        return ids.Count == 0 ? null : string.Join(' ', ids);
    }

    private static bool Contains(IReadOnlyList<string> values, string identity)
    {
        for (int i = 0; i < values.Count; i++)
        {
            if (string.Equals(values[i], identity, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>The phase of a parent box's three-step cycle through a mixed selection.</summary>
internal enum ParentCycle
{
    /// <summary>The next toggle selects all.</summary>
    Mixed,

    /// <summary>The next toggle selects none.</summary>
    On,

    /// <summary>The next toggle restores the snapshot.</summary>
    Off,
}

/// <summary>A parent toggle's proposed value and the cycle status that follows when it is not canceled.</summary>
/// <param name="Value">The proposed value.</param>
/// <param name="NextStatus">The status to move to, or <see langword="null"/> when the cycle does not advance.</param>
internal readonly record struct ParentProposal(IReadOnlyList<string> Value, ParentCycle? NextStatus);
