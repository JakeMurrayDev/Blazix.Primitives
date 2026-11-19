using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// The item of a registration-only list as a part uses it (CMP §6.5, useCompositeListItem.ts; M3 track 1, E1, ADR 04
// Amendments 2026-10-26, chosen by the user): TabsPanel and AccordionItem. Composed into the part, never a nested
// component (ADR 16 rule 10).
// - It registers the id its part renders, in the batch that creates the part, and unregisters on dispose. Every list
//   item renders an id, the consumer's or a generated one, so the module can resolve it (design point 2, option (a),
//   settled by the orchestrator 2026-10-26).
// - It renders nothing of its own: no tabindex and no handler (a TabsPanel renders its own tabindex, TAB-DOM-15/16).
// - Its index is -1 until the list's first report and in prerender (CMP-STA-15), then the item's position in the
//   reported order; the part re-renders only when the index changes (ADR 16 rule 8).
// - Without a list it is inert, unlike a composite item (CMP-STR-02): index -1, nothing registered, a dispose that does not
//   throw (CMP-STR-03; Base UI's no-op default context, CompositeListContext.ts:18-23).
/// <summary>The item mechanism of a registration-only list: its registration and the index the list reports for it.</summary>
internal sealed class CompositeListItemBehavior : IDisposable
{
    private readonly CompositeListContext? list;
    private readonly Action indexChanged;
    private readonly bool guess;
    private string? elementId;
    private bool registered;

    /// <summary>Initializes a new instance of the <see cref="CompositeListItemBehavior"/> class.</summary>
    /// <param name="context">The cascaded registry of the nearest list, or <see langword="null"/> for an inert item.</param>
    /// <param name="indexChanged">Renders the part again; called when the reported index changes.</param>
    /// <param name="guess">Whether the item takes its index from the list's render order until the first report.</param>
    public CompositeListItemBehavior(CompositeListContext? context, Action indexChanged, bool guess = false)
    {
        list = context;
        this.indexChanged = indexChanged;
        this.guess = guess;
    }

    /// <summary>Gets the item's index in its list.</summary>
    /// <value>The position of the item in the order the list last reported; -1 before the first report and without a list.</value>
    public int Index { get; private set; } = -1;

    // M6 track 4 (decision 23 of the M6 plan, chosen by the user 2026-11-18; the Composite/ grant to Slider, ADR 04 note of
    // 2026-11-18): a slider thumb with an Index registers with it, so the module and C# address the thumb by one index.
    /// <summary>Gets the explicit index the item registered with.</summary>
    /// <value>The index the item occupies, or <see langword="null"/> for its position in document order.</value>
    public int? ExplicitIndex { get; private set; }

    /// <summary>Gets the id the item registered.</summary>
    /// <value>The id the part renders on its element; empty before the first parameter set.</value>
    public string Id { get; private set; } = string.Empty;

    /// <inheritdoc />
    public void Dispose()
    {
        if (!registered)
        {
            return;
        }

        registered = false;
        list!.Unregister(this);
    }

    /// <summary>Registers the item, or records a change of its id or its explicit index; called on every parameter set.</summary>
    /// <param name="id">The id the part renders on its element.</param>
    /// <param name="index">The explicit index the item occupies, or <see langword="null"/> for its position in document order.</param>
    public void SetRegistration(string? id, int? index = null)
    {
        if (list is null)
        {
            return;
        }

        string value = id ?? string.Empty;
        if (!registered)
        {
            Id = value;
            ExplicitIndex = index;
            registered = true;

            // M6 track 7 (CMP-STA-16; useCompositeListItem.ts:39-55, the `guess` option; Q-OTP-2 (b) of the M6 plan, chosen by
            // the user 2026-11-18; the Composite/ grant to OtpField, settled by the orchestrator 2026-11-19): a guessing item
            // takes the next index of the list's render-order counter once, before it registers, and keeps it until a report
            // names it; an explicitly indexed item takes none. Prerender has no report, so it renders the guess (OTP-EDGE-10).
            if (guess && index is null && Index == -1)
            {
                Index = list.TakeGuess();
            }

            list.Register(this);
            return;
        }

        if (!string.Equals(Id, value, StringComparison.Ordinal) || ExplicitIndex != index)
        {
            string previousId = Id;
            Id = value;
            ExplicitIndex = index;
            list.RegistrationChanged(this, previousId);
        }
    }

    /// <summary>Reports a replaced element and asks the list to send what changed.</summary>
    /// <param name="element">The part's element, or <see langword="null"/> when it has no element reference.</param>
    /// <returns>A task that completes when the list's browser-side behavior has the change.</returns>
    public Task AfterRenderAsync(ElementReference? element)
    {
        if (list is null)
        {
            return Task.CompletedTask;
        }

        string? current = element?.Id;
        if (current is not null)
        {
            if (elementId is not null && !string.Equals(elementId, current, StringComparison.Ordinal))
            {
                list.ElementReplaced();
            }

            elementId = current;
        }

        return list.FlushAsync();
    }

    /// <summary>Sets the index the list reported, rendering the part when it changed.</summary>
    /// <param name="index">The item's position in the reported order, or -1.</param>
    internal void SetIndex(int index)
    {
        if (index == Index)
        {
            return;
        }

        Index = index;
        indexChanged();
    }
}
