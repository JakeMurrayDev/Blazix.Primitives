using System.Text;
using Blazix.Primitives.Utilities;

namespace Blazix.Primitives;

// FFI §2 (labelable scope), FFI-STR-03, FFI-ALG-06 (control-id selection), FFI-ALG-09 (described-by composition),
// FFI-ARIA-02 (message ids), FFI-ARIA-08 (label id), FFI-FRM-35 (a scope with no registration keeps its selection);
// internals/labelable-provider/LabelableProvider.tsx. Opened by FieldRoot and by each FieldItem. Every change runs the
// owning field's notification, so the parts of one field re-render once per operation.
/// <summary>The labelable scope: the control id a label points at, the label id a control references, and the description ids.</summary>
internal sealed class LabelableScope
{
    private readonly LabelableScope? parent;
    private readonly Action changed;
    private readonly List<KeyValuePair<object, string?>> registrations = [];
    private readonly List<string> messageIds = [];
    private string? controlId;
    private int version;
    private string? describedByInput;
    private int describedByVersion = -1;
    private string? describedBy;

    /// <summary>Initializes a new instance of the <see cref="LabelableScope"/> class.</summary>
    /// <param name="defaultId">The generated id a control renders before it registers its own, and the label's <c>for</c> until then.</param>
    /// <param name="parent">The enclosing scope, whose description ids come first, or <see langword="null"/>.</param>
    /// <param name="changed">Runs after the control id, the label id or the description ids changed.</param>
    public LabelableScope(string defaultId, LabelableScope? parent, Action changed)
    {
        DefaultId = defaultId;
        controlId = defaultId;
        this.parent = parent;
        this.changed = changed;
        Label = new LabelIdSlot { Changed = changed };
    }

    private int Version => version + (parent?.Version ?? 0);

    /// <summary>Gets the generated id the scope selects while no control has registered one.</summary>
    /// <value>The default id.</value>
    public string DefaultId { get; }

    /// <summary>Gets the selected control id.</summary>
    /// <value>The id a native label's <c>for</c> points at, or <see langword="null"/> after a control claimed no id.</value>
    public string? ControlId => controlId;

    /// <summary>Gets the slot the label publishes its id to.</summary>
    /// <value>The label-id slot.</value>
    public LabelIdSlot Label { get; }

    /// <summary>Gets the ids of the descriptions and errors published to this scope, in publication order.</summary>
    /// <value>The message ids.</value>
    public IReadOnlyList<string> MessageIds => messageIds;

    // FFI-ALG-06 registerControlId(source, nextId) with a defined id; `null` is a claim that suppresses `for`.
    /// <summary>Registers the id a control claims, keeping the current selection while it is still registered.</summary>
    /// <param name="source">The control.</param>
    /// <param name="id">The id, or <see langword="null"/> for a control named by <c>aria-labelledby</c> instead.</param>
    public void RegisterControlId(object source, string? id)
    {
        int index = IndexOf(source);
        if (index >= 0)
        {
            registrations[index] = new KeyValuePair<object, string?>(source, id);
        }
        else
        {
            registrations.Add(new KeyValuePair<object, string?>(source, id));
        }

        Select();
    }

    // FFI-ALG-06 registerControlId(source, undefined).
    /// <summary>Removes a control's claim; the scope keeps its selection when no claim remains.</summary>
    /// <param name="source">The control.</param>
    public void UnregisterControlId(object source)
    {
        int index = IndexOf(source);
        if (index < 0)
        {
            return;
        }

        registrations.RemoveAt(index);
        Select();
    }

    // FFI-ALG-06 resetControlId.
    /// <summary>Selects the default id again when no control has a claim.</summary>
    public void ResetControlId()
    {
        if (registrations.Count == 0)
        {
            SetControlId(DefaultId);
        }
    }

    // FFI-ARIA-02; field/description/FieldDescription.tsx:36-46 (`setMessageIds((v) => v.concat(id))`).
    /// <summary>Publishes a description or error id.</summary>
    /// <param name="id">The id; an empty id is never published.</param>
    public void AddMessageId(string id)
    {
        if (id.Length == 0)
        {
            return;
        }

        messageIds.Add(id);
        version++;
        changed();
    }

    // `setMessageIds((v) => v.filter((item) => item !== id))`: every copy of the id goes.
    /// <summary>Withdraws a description or error id.</summary>
    /// <param name="id">The id.</param>
    public void RemoveMessageId(string id)
    {
        if (messageIds.RemoveAll(item => string.Equals(item, id, StringComparison.Ordinal)) > 0)
        {
            version++;
            changed();
        }
    }

    // FFI-ALG-09: the consumer's ids (split on spaces), then the parent scope's message ids, then this scope's, without
    // duplicates, joined by spaces; null when empty. Memoised on the consumer string and the scopes' versions.
    /// <summary>Composes a control's <c>aria-describedby</c>.</summary>
    /// <param name="consumer">The consumer's <c>aria-describedby</c>, or <see langword="null"/>.</param>
    /// <returns>The composed ids, or <see langword="null"/> when there is none.</returns>
    public string? DescribedBy(string? consumer)
    {
        int current = Version;
        if (current == describedByVersion && string.Equals(consumer, describedByInput, StringComparison.Ordinal))
        {
            return describedBy;
        }

        List<string> ids = [];
        if (!string.IsNullOrEmpty(consumer))
        {
            foreach (string part in consumer.Split(' '))
            {
                AddDistinct(ids, part);
            }
        }

        if (parent is not null)
        {
            foreach (string id in parent.MessageIds)
            {
                AddDistinct(ids, id);
            }
        }

        foreach (string id in messageIds)
        {
            AddDistinct(ids, id);
        }

        StringBuilder? builder = null;
        foreach (string id in ids)
        {
            if (id.Length == 0)
            {
                continue;
            }

            builder = builder is null ? new StringBuilder(id) : builder.Append(' ').Append(id);
        }

        describedBy = builder?.ToString();
        describedByInput = consumer;
        describedByVersion = current;
        return describedBy;
    }

    private static void AddDistinct(List<string> ids, string id)
    {
        if (!ids.Contains(id))
        {
            ids.Add(id);
        }
    }

    private int IndexOf(object source)
    {
        for (int i = 0; i < registrations.Count; i++)
        {
            if (ReferenceEquals(registrations[i].Key, source))
            {
                return i;
            }
        }

        return -1;
    }

    // FFI-ALG-06 `select(prev)`: keep the current selection while it is registered, else the first registration's id; with
    // no registration the previous selection stays (a control whose subtree is gone keeps its label association).
    private void Select()
    {
        if (registrations.Count == 0)
        {
            return;
        }

        foreach (KeyValuePair<object, string?> registration in registrations)
        {
            if (string.Equals(registration.Value, controlId, StringComparison.Ordinal))
            {
                return;
            }
        }

        SetControlId(registrations[0].Value);
    }

    private void SetControlId(string? id)
    {
        if (string.Equals(controlId, id, StringComparison.Ordinal))
        {
            return;
        }

        controlId = id;
        changed();
    }
}
