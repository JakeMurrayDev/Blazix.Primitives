using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// The item of a registration-only list as a part uses it (CMP §6.5, useCompositeListItem.ts; M3 track 1, E1, ADR 04
// Amendments 2026-10-26, chosen by the user): TabsPanel and AccordionItem. Composed into the part, never a nested
// component (ADR 16 rule 10).
// - It registers the id its part renders, in the batch that creates the part, and unregisters on dispose. Every list
//   item renders an id, the consumer's or a generated one, so the module can resolve it (design point 2, option (a),
//   pending the user).
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
    private string? elementId;
    private bool registered;

    /// <summary>Initializes a new instance of the <see cref="CompositeListItemBehavior"/> class.</summary>
    /// <param name="context">The cascaded registry of the nearest list, or <see langword="null"/> for an inert item.</param>
    /// <param name="indexChanged">Renders the part again; called when the reported index changes.</param>
    public CompositeListItemBehavior(CompositeListContext? context, Action indexChanged)
    {
        list = context;
        this.indexChanged = indexChanged;
    }

    /// <summary>Gets the item's index in its list.</summary>
    /// <value>The position of the item in the order the list last reported; -1 before the first report and without a list.</value>
    public int Index { get; private set; } = -1;

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

    /// <summary>Registers the item, or records a change of its id; called on every parameter set.</summary>
    /// <param name="id">The id the part renders on its element.</param>
    public void SetRegistration(string? id)
    {
        if (list is null)
        {
            return;
        }

        string value = id ?? string.Empty;
        if (!registered)
        {
            Id = value;
            registered = true;
            list.Register(this);
            return;
        }

        if (!string.Equals(Id, value, StringComparison.Ordinal))
        {
            string previousId = Id;
            Id = value;
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
