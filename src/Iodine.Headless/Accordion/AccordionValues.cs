using Blazix.Primitives.Store;

namespace Blazix.Primitives;

// Base UI's value algorithm: handleValueChange (accordion/root/AccordionRoot.tsx:67-89) and the membership test
// openValues.includes(value) (accordion/item/AccordionItem.tsx:48-54). ACC-STA-04 (SameValueZero membership), ACC-STA-05
// (single mode: value[0] === id ? [] : [id], the direction not consulted), ACC-STA-06 (multiple: append on open, a strict
// filter on close), ACC-API-10/ACC-EDGE-09 (the identity: the value unless absent or null, else the generated id under
// string). Settled by the orchestrator 2026-10-25 (M3 plan, Settled): loops with the Equality helpers, never Contains or
// LINQ, which box on the interpreter and compare records structurally.
/// <summary>The pure rules of an accordion's value: which items are open and the value a request asks for.</summary>
internal static class AccordionValues
{
    // ACC-API-10, ACC-EDGE-09 (the R7 pattern: a part-misuse error in every build).
    internal const string MissingValueMessage =
        "Blazix: An `AccordionItem` without a `Value` is identified by a generated id, which needs a `TValue` of `string`. "
        + "Give the item a `Value`, or set `TValue=\"string\"` on the `AccordionRoot`.";

    /// <summary>Returns the shared empty value, which a root without a default value starts from.</summary>
    /// <typeparam name="TValue">The type of the items' values.</typeparam>
    /// <returns>An empty list that cannot be changed.</returns>
    public static IReadOnlyList<TValue> Empty<TValue>() => Array.Empty<TValue>();

    /// <summary>Determines whether an item is open: whether the value contains its identity by SameValueZero.</summary>
    /// <typeparam name="TValue">The type of the items' values.</typeparam>
    /// <param name="value">The root's value.</param>
    /// <param name="identity">The item's identity.</param>
    /// <returns><see langword="true"/> when an element of <paramref name="value"/> is the same as <paramref name="identity"/>.</returns>
    public static bool Contains<TValue>(IReadOnlyList<TValue> value, TValue identity)
    {
        for (int i = 0; i < value.Count; i++)
        {
            if (Equality.SameValueZero(value[i], identity))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns the value an item's request asks for.</summary>
    /// <typeparam name="TValue">The type of the items' values.</typeparam>
    /// <param name="current">The root's current value.</param>
    /// <param name="identity">The identity of the item that requested the change.</param>
    /// <param name="nextOpen">The open state the item asked for; single mode does not consult it.</param>
    /// <param name="multiple">Whether several items can be open at once.</param>
    /// <returns>A new list: never <paramref name="current"/>, which is not changed.</returns>
    public static IReadOnlyList<TValue> Next<TValue>(IReadOnlyList<TValue> current, TValue identity, bool nextOpen, bool multiple)
    {
        if (!multiple)
        {
            return current.Count > 0 && Equality.StrictEquals(current[0], identity) ? Array.Empty<TValue>() : [identity];
        }

        if (nextOpen)
        {
            // Not a flattening concatenation: an identity that is itself a list is appended as one element.
            TValue[] appended = new TValue[current.Count + 1];
            for (int i = 0; i < current.Count; i++)
            {
                appended[i] = current[i];
            }

            appended[^1] = identity;
            return appended;
        }

        List<TValue> kept = new(current.Count);
        for (int i = 0; i < current.Count; i++)
        {
            if (!Equality.StrictEquals(current[i], identity))
            {
                kept.Add(current[i]);
            }
        }

        return kept;
    }

    /// <summary>Returns an item's identity: its value, unless that is absent or <see langword="null"/>.</summary>
    /// <typeparam name="TValue">The type of the items' values.</typeparam>
    /// <param name="valueSupplied">Whether the item's <c>Value</c> parameter was supplied.</param>
    /// <param name="value">The item's <c>Value</c>.</param>
    /// <param name="fallback">The id the item drew, its identity when it has no value.</param>
    /// <returns><paramref name="value"/>, or <paramref name="fallback"/> when <typeparamref name="TValue"/> is <see cref="string"/>.</returns>
    /// <exception cref="InvalidOperationException">The item has no value and <typeparamref name="TValue"/> is not <see cref="string"/>.</exception>
    public static TValue Identity<TValue>(bool valueSupplied, TValue value, string fallback)
    {
        if (valueSupplied && value is not null)
        {
            return value;
        }

        if (typeof(TValue) == typeof(string))
        {
            return (TValue)(object)fallback;
        }

        throw new InvalidOperationException(MissingValueMessage);
    }
}
