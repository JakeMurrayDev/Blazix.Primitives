using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// A root-less item registry as a part uses it (CMP §6.6, CompositeList without a CompositeRoot; CMP-STR-03): TabsRoot's
// panels (TabsRoot.tsx:346) and AccordionRoot's items (AccordionRoot.tsx:121). M3 track 1, E1 (ADR 04 Amendments
// 2026-10-26, chosen by the user). Composed into the part as CompositeRootBehavior is, never a nested component (ADR 16
// rule 10), and attached the same way: by Element, by the generated id under a substitute callback, or by a late id.
//
// JavaScript owns the order (ADR 01 decision 2): the composite module attaches in its registration-only mode, which
// keeps the registry, its move detection and its shadow-root items, and reports the items' ids in index order on each
// publication that changed them. It installs no listener, writes no tabindex and keeps no highlight. C# keeps the items
// by id, sends one `update` per render batch that changed the ids or an item's element (CMP-STA-10; Server: after paint
// plus a round trip, ADR 01's row "EVD-STA-07, CMP-STA-10, FFI registration", which names the published indexes), and
// sets each item's index from the report: its position, or -1 when the report does not name it. Each report is the whole
// sequence, joined when it arrives, before or after the handle exists; an id no registration carries is ignored
// (design points 1 and 3, option (a), pending the user). The first registration of an id is the one sent and joined, as
// the root's.
/// <summary>
/// The registration-only list mechanism of one part: its <see cref="CompositeTwin"/> in registration-only mode, the
/// registry its items join through <see cref="Context"/>, and the indexes the module reports for them.
/// </summary>
internal sealed class CompositeListBehavior : IDisposable
{
    private readonly IJSRuntime jsRuntime;
    private readonly PartIdGenerator idGenerator;
    private readonly Action stateChanged;
    private readonly TaskCompletionSource attachedSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<CompositeListItemBehavior> items = [];
    private CompositeTwin? twin;
    private TwinHandle<CompositeOptions>? handle;
    private bool sent;
    private int itemsVersion;
    private int sentItemsVersion;
    private bool elementReplaced;
    private bool attaching;
    private bool renderPending;
    private bool disposed;
    private string? lateId;

    /// <summary>Occurs once per report of the items' order, after every item holds its new index.</summary>
    public event Action? ItemsChanged;

    /// <summary>Initializes a new instance of the <see cref="CompositeListBehavior"/> class.</summary>
    /// <param name="jsRuntime">The runtime the twin imports its module through.</param>
    /// <param name="idGenerator">The source of the late id of a component root.</param>
    /// <param name="stateChanged">Renders the part again; called to render a late id or a removal.</param>
    public CompositeListBehavior(IJSRuntime jsRuntime, PartIdGenerator idGenerator, Action stateChanged)
    {
        this.jsRuntime = jsRuntime;
        this.idGenerator = idGenerator;
        this.stateChanged = stateChanged;
        Context = new CompositeListContext(this);
    }

    /// <summary>Gets the registry the part cascades to its items.</summary>
    /// <value>The context, cascaded with <c>IsFixed</c> inside the list's element.</value>
    public CompositeListContext Context { get; }

    /// <summary>Gets the id the behavior drew for a list element that a component root rendered.</summary>
    /// <value>The late id, or <see langword="null"/> while none was needed.</value>
    public string? LateId => lateId;

    /// <summary>Gets a task that completes when the behavior is attached to the list element.</summary>
    /// <value>A task that completes after the first interactive render has attached the behavior.</value>
    public Task TwinAttached => attachedSource.Task;

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (twin is not null)
        {
            twin.ItemsChanged -= OnItemsChange;
            _ = twin.DisposeAsync().AsTask();
            twin = null;
            handle = null;
        }
    }

    /// <summary>Adds the list's attributes to a render: a late id when one was drawn.</summary>
    /// <param name="attributes">The part-supplied attributes.</param>
    /// <param name="consumerId">The part's id when the consumer supplied one or a generated one was drawn.</param>
    public void AddAttributes(AttributeList attributes, string? consumerId)
    {
        // The list rendered in this batch, so its after-render sends whatever the batch changed.
        renderPending = true;

        if (lateId is not null && consumerId is null)
        {
            attributes.Set("id", lateId);
        }
    }

    /// <summary>Attaches the twin after the first render that showed the list element, then sends items that changed.</summary>
    /// <param name="enabled">Whether the part rendered its element.</param>
    /// <param name="element">The element, or <see langword="null"/> under a substitute callback or component root.</param>
    /// <param name="id">The part's id (the consumer's or the generated one), or <see langword="null"/>.</param>
    /// <returns>A task that completes when the module has the items.</returns>
    public async Task AfterRenderAsync(bool enabled, ElementReference? element, string? id)
    {
        renderPending = false;
        if (disposed)
        {
            return;
        }

        if (!enabled)
        {
            // Nothing is rendered: the element the module observed is gone.
            if (handle is { } released)
            {
                handle = null;
                sent = false;
                await released.DisposeAsync();
            }

            return;
        }

        if (handle is not null)
        {
            await FlushAsync();
            return;
        }

        if (attaching)
        {
            return;
        }

        string? elementId = null;
        if (element is null)
        {
            elementId = id ?? lateId;
            if (elementId is null)
            {
                // A substitute whose root is a component leaves no element reference: render an id, attach next time.
                lateId = idGenerator.Next();
                stateChanged();
                return;
            }
        }

        twin ??= CreateTwin();
        CompositeOptions options = BuildOptions();
        attaching = true;
        try
        {
            handle = elementId is null
                ? await twin.AttachAsync(element!.Value, options)
                : await twin.AttachAsync(elementId, options);
        }
        finally
        {
            attaching = false;
        }

        attachedSource.TrySetResult();

        // Registrations that arrived while the attach was in flight.
        await FlushAsync();
    }

    /// <summary>Adds an item to the registry.</summary>
    /// <param name="item">The item, whose id is set.</param>
    internal void Register(CompositeListItemBehavior item)
    {
        items.Add(item);
        if (IsFirstWithId(item, item.Id))
        {
            itemsVersion++;
        }
    }

    /// <summary>Removes an item from the registry; the list renders unless it already rendered in this batch.</summary>
    /// <param name="item">The item.</param>
    internal void Unregister(CompositeListItemBehavior item)
    {
        bool first = IsFirstWithId(item, item.Id);
        if (!items.Remove(item) || !first)
        {
            return;
        }

        itemsVersion++;
        if (!disposed && !renderPending && (handle is not null || attaching))
        {
            renderPending = true;
            stateChanged();
        }
    }

    /// <summary>Records that a registered item changed its id.</summary>
    /// <param name="item">The item, whose id is already the new one.</param>
    /// <param name="previousId">The item's previous id.</param>
    internal void RegistrationChanged(CompositeListItemBehavior item, string previousId)
    {
        if (IsFirstWithId(item, previousId) || IsFirstWithId(item, item.Id))
        {
            itemsVersion++;
        }
    }

    /// <summary>Records that a registered item rendered a new element under the same id, which the module must resolve again.</summary>
    internal void ElementReplaced() => elementReplaced = true;

    /// <summary>Sends the items if they changed since the last send.</summary>
    /// <returns>A task that completes when the module has the items.</returns>
    internal Task FlushAsync()
    {
        if (handle is null || disposed || (sent && !elementReplaced && sentItemsVersion == itemsVersion))
        {
            return Task.CompletedTask;
        }

        return handle.UpdateAsync(BuildOptions()).AsTask();
    }

    // Whether no item registered before this one carries the id, so the list sends this item's registration for it.
    private bool IsFirstWithId(CompositeListItemBehavior item, string id)
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (ReferenceEquals(items[i], item))
            {
                return true;
            }

            if (string.Equals(items[i].Id, id, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return false;
    }

    private CompositeTwin CreateTwin()
    {
        CompositeTwin created = new(jsRuntime);
        created.ItemsChanged += OnItemsChange;
        return created;
    }

    private CompositeOptions BuildOptions()
    {
        int count = 0;
        for (int i = 0; i < items.Count; i++)
        {
            if (IsFirstWithId(items[i], items[i].Id))
            {
                count++;
            }
        }

        CompositeItemRegistration[] registrations = new CompositeItemRegistration[count];
        int next = 0;
        for (int i = 0; i < items.Count; i++)
        {
            if (IsFirstWithId(items[i], items[i].Id))
            {
                registrations[next++] = new CompositeItemRegistration(items[i].Id, null);
            }
        }

        sent = true;
        sentItemsVersion = itemsVersion;
        elementReplaced = false;
        return new CompositeOptions { RegistrationOnly = true, Items = registrations };
    }

    // Sets every item's index from the report: its position, or -1 when the report does not name it. Only an item whose
    // index changed renders again.
    private void OnItemsChange(IReadOnlyList<string> ids)
    {
        Dictionary<string, int> positions = new(ids.Count, StringComparer.Ordinal);
        for (int i = 0; i < ids.Count; i++)
        {
            positions.TryAdd(ids[i], i);
        }

        // Only the first registration of an id is joined, as only it was sent.
        HashSet<string> seen = new(StringComparer.Ordinal);
        for (int i = 0; i < items.Count; i++)
        {
            CompositeListItemBehavior item = items[i];
            item.SetIndex(seen.Add(item.Id) && positions.TryGetValue(item.Id, out int position) ? position : -1);
        }

        ItemsChanged?.Invoke();
    }
}
