namespace Blazix.Primitives;

// TGG §4's value-rule table; toggle-group/ToggleGroup.tsx:61-71 (setGroupValue). TGG-STA-03: single-select gives
// [identity] or [], so the last pressed toggle may be deselected (toggle-group §16 Q1, chosen by the user 2026-10-26: mirror
// the source). TGG-STA-04: multi-select appends to a copy or removes the first occurrence from a copy. TGG-EVT-05: every
// call returns a new list and never mutates the current one (ADR 08 Amendments 2026-10-22: a commit produces a new list).
// Membership is ordinal (`indexOf`); an identity that matches no toggle is kept by multi-select and dropped by single-select
// (TGG-EDGE-05), and a duplicated identity loses its first occurrence only (TGG-EDGE-04). Base UI's `splice(indexOf(v), 1)`
// would remove the last entry when the identity is absent; a toggle asks to unpress only while its identity is in the value,
// so the port copies the value unchanged there.
/// <summary>Computes the next value of a toggle group from one toggle's activation.</summary>
internal static class ToggleGroupValue
{
    /// <summary>Returns the value after the toggle with <paramref name="identity"/> asked to become <paramref name="nextPressed"/>.</summary>
    /// <param name="current">The group's current value, which is not changed.</param>
    /// <param name="identity">The identity of the activated toggle.</param>
    /// <param name="nextPressed"><see langword="true"/> when the toggle asks to become pressed.</param>
    /// <param name="multiple"><see langword="true"/> when any number of toggles may be pressed.</param>
    /// <returns>A new list.</returns>
    public static IReadOnlyList<string> Next(IReadOnlyList<string> current, string identity, bool nextPressed, bool multiple)
    {
        if (!multiple)
        {
            // A new empty array rather than the shared one, so that even an emptied value is a new list.
            return nextPressed ? new[] { identity } : new string[0];
        }

        if (nextPressed)
        {
            string[] appended = new string[current.Count + 1];
            for (int i = 0; i < current.Count; i++)
            {
                appended[i] = current[i];
            }

            appended[current.Count] = identity;
            return appended;
        }

        int index = -1;
        for (int i = 0; i < current.Count; i++)
        {
            if (string.Equals(current[i], identity, StringComparison.Ordinal))
            {
                index = i;
                break;
            }
        }

        string[] remaining = new string[index < 0 ? current.Count : current.Count - 1];
        for (int i = 0, j = 0; i < current.Count; i++)
        {
            if (i != index)
            {
                remaining[j++] = current[i];
            }
        }

        return remaining;
    }
}
