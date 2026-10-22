using Blazix.Primitives.Utilities;

namespace Blazix.Primitives;

// FST-STR-01..03, FST-API-01, FST-DOM-07, FST-FRM-03, FFI-STR-06; fieldset/root/FieldsetRootContext.ts. The fieldset scope:
// the legend id (LabelIdSlot, FFI-ARIA-08) and the effective disabled state, which a nested root ORs with its parent's and
// follows in both directions (FST-STA-03). Cascaded fixed by FieldsetRoot around its element (ADR 16 rule 9), so a
// substituted root component sees it (FST-DOM-09); readers subscribe.
/// <summary>The fieldset scope of one <see cref="FieldsetRoot"/>: its legend id and its effective disabled state.</summary>
internal sealed class FieldsetRootContext : IDisposable
{
    private readonly ChangeListeners listeners = new();
    private readonly FieldsetRootContext? parent;
    private readonly Action parentChanged;
    private bool disabledParameter;

    /// <summary>Initializes a new instance of the <see cref="FieldsetRootContext"/> class.</summary>
    /// <param name="parent">The nearest enclosing fieldset's scope, or <see langword="null"/>.</param>
    public FieldsetRootContext(FieldsetRootContext? parent)
    {
        this.parent = parent;
        parentChanged = Notify;
        parent?.AddListener(parentChanged);
        Legend = new LabelIdSlot { Changed = Notify };
    }

    /// <summary>Gets the slot the legend publishes its id to.</summary>
    /// <value>The legend id slot.</value>
    public LabelIdSlot Legend { get; }

    // FST-API-01, FST-STR-03: `parentDisabled || disabled`.
    /// <summary>Gets a value indicating whether the fieldset is disabled, by its own parameter or an enclosing fieldset.</summary>
    /// <value>The effective disabled state.</value>
    public bool Disabled => (parent?.Disabled ?? false) || disabledParameter;

    /// <inheritdoc />
    /// <remarks>Stops following the enclosing fieldset.</remarks>
    public void Dispose()
    {
        parent?.RemoveListener(parentChanged);
        Legend.Changed = null;
        listeners.Clear();
    }

    // FST-STR-02, FST-EDGE-01 (PRG-EDGE-01 precedent: the port's words, `Blazix: `).
    /// <summary>Throws the error of a part rendered outside a fieldset root.</summary>
    /// <param name="context">The cascaded context, or <see langword="null"/>.</param>
    /// <returns>The context.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public static FieldsetRootContext Require(FieldsetRootContext? context)
    {
        return context ?? throw new InvalidOperationException(
            "Blazix: The `FieldsetRoot` context is missing. Fieldset parts must be placed within `<FieldsetRoot>`.");
    }

    /// <summary>Sets the fieldset's own disabled parameter.</summary>
    /// <param name="disabled">The disabled parameter.</param>
    public void SetDisabled(bool disabled)
    {
        if (disabledParameter == disabled)
        {
            return;
        }

        bool before = Disabled;
        disabledParameter = disabled;
        if (before != Disabled)
        {
            Notify();
        }
    }

    /// <summary>Adds a listener that runs after the legend id or the effective disabled state changed.</summary>
    /// <param name="listener">The listener.</param>
    public void AddListener(Action listener) => listeners.Add(listener);

    /// <summary>Removes a listener added with <see cref="AddListener"/>.</summary>
    /// <param name="listener">The listener.</param>
    public void RemoveListener(Action listener) => listeners.Remove(listener);

    private void Notify()
    {
        listeners.Notify();
    }
}
