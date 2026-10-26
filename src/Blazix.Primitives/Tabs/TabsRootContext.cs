using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// TAB §11 (TabsRootContext); tabs/root/TabsRootContext.ts. A plain listener list cascaded fixed (ADR 16 rule 9; the
// Switch, Checkbox and RadioGroup groups' shape): a change of the value, the orientation, the mirrored direction or the
// panel registry notifies every part, which compares what it renders and renders only when that changed, so a selection
// renders the two Tabs and the two Panels whose state flipped and never the Root's subtree (ADR 16 rules 8 and 9). The
// non-generic base is what TabsList and TabsIndicator read and what a part whose TValue differs from its Root's receives
// (the RadioGroup R7 precedent, ADR 08 Amendments 2026-10-26).
/// <summary>The state a tabs root shares with every part in its subtree, independent of the type of its value.</summary>
internal abstract class TabsRootContext
{
    private readonly List<Action> listeners = [];
    private readonly List<Action<bool>> tabListeners = [];

    /// <summary>Initializes a new instance of the <see cref="TabsRootContext"/> class.</summary>
    /// <param name="controlled">Whether the consumer owns the root's value.</param>
    protected TabsRootContext(bool controlled)
    {
        Controlled = controlled;
    }

    /// <summary>Gets a value indicating whether the consumer owns the root's value.</summary>
    /// <value>Fixed at the root's first parameter set.</value>
    public bool Controlled { get; }

    /// <summary>Gets the axis along which the tabs are laid out.</summary>
    /// <value>The root's orientation.</value>
    public Orientation Orientation { get; private set; }

    // ADR 04 Amendments 2026-10-26: the module writes data-activation-direction; this is C#'s mirror (RND-STA-01 row).
    /// <summary>Gets the activation direction the browser-side behavior last reported.</summary>
    /// <value><see cref="TabsActivationDirection.None"/> until a report arrives.</value>
    public TabsActivationDirection Direction { get; private set; } = TabsActivationDirection.None;

    // TAB-DOM-19: the Indicator renders nothing while the value is null.
    /// <summary>Gets a value indicating whether the root holds a value that is not <see langword="null"/>.</summary>
    /// <value><see langword="true"/> while some value is selected, whether or not a tab has it.</value>
    public abstract bool HasValue { get; }

    /// <summary>Throws the error of a tabs part rendered outside a tabs root.</summary>
    /// <param name="context">The cascaded context, or <see langword="null"/>.</param>
    /// <returns>The context.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public static TabsRootContext Require(TabsRootContext? context)
    {
        // TAB-EDGE-01: thrown in every build (ADR 02 Amendments 2026-10-20), the RadioIndicator rewrite as precedent.
        return context ?? throw new InvalidOperationException(
            "Blazix: The `TabsRoot` context is missing. Tabs parts must be placed within `<TabsRoot>`.");
    }

    /// <summary>Adds a listener that runs after every change of the root's shared state.</summary>
    /// <param name="listener">The listener.</param>
    public void AddListener(Action listener) => listeners.Add(listener);

    /// <summary>Removes a listener added with <see cref="AddListener"/>.</summary>
    /// <param name="listener">The listener.</param>
    public void RemoveListener(Action listener) => listeners.Remove(listener);

    // TAB-STA-14 with ADR 04 Amendments 2026-10-26: outside an event dispatch (the `tabs` module's report on Server) each
    // part's render is a render batch of its own, and the module reads `[data-active]` after each. A tab's listener runs
    // twice, so the Tab that becomes active renders in the first pass, before the one it replaces renders in the second,
    // and no batch shows a selection change with no active Tab.
    /// <summary>Adds a tab's listener, which runs in both passes of every change of the root's shared state.</summary>
    /// <param name="listener">
    /// The listener, which receives <see langword="true"/> in the first pass, where only a tab that becomes active renders,
    /// and <see langword="false"/> in the second, before the other listeners.
    /// </param>
    public void AddTabListener(Action<bool> listener) => tabListeners.Add(listener);

    /// <summary>Removes a listener added with <see cref="AddTabListener"/>.</summary>
    /// <param name="listener">The listener.</param>
    public void RemoveTabListener(Action<bool> listener) => tabListeners.Remove(listener);

    /// <summary>Takes the root's orientation and notifies the parts when it changed.</summary>
    /// <param name="orientation">The orientation.</param>
    public void SetOrientation(Orientation orientation)
    {
        if (orientation == Orientation)
        {
            return;
        }

        Orientation = orientation;
        Notify();
    }

    /// <summary>Takes the activation direction the browser-side behavior reported and notifies the parts when it changed.</summary>
    /// <param name="direction">The direction.</param>
    public void MirrorDirection(TabsActivationDirection direction)
    {
        if (direction == Direction)
        {
            return;
        }

        Direction = direction;
        Notify();
    }

    /// <summary>Runs every listener: the tabs' first pass, then their second, then the other listeners.</summary>
    protected void Notify()
    {
        // A listener may remove itself or another (a part disposed by the render it triggers): iterating backwards with a
        // bounds check never skips a listener that is still registered.
        for (int i = tabListeners.Count - 1; i >= 0; i--)
        {
            if (i < tabListeners.Count)
            {
                tabListeners[i](true);
            }
        }

        for (int i = tabListeners.Count - 1; i >= 0; i--)
        {
            if (i < tabListeners.Count)
            {
                tabListeners[i](false);
            }
        }

        for (int i = listeners.Count - 1; i >= 0; i--)
        {
            if (i < listeners.Count)
            {
                listeners[i]();
            }
        }
    }
}

// TAB-STA-01..03, TAB-STA-21, TAB-API-07, TAB-EDGE-07; tabs/root/TabsRoot.tsx:137-167 (the mounted-panel registry). ADR 08
// Amendments 2026-10-22 and 2026-10-26: TValue, compared with Equality.StrictEquals; `null` means nothing selected; the
// implicit default (TabsImplicitDefault) until the first commit.
/// <summary>The state a tabs root shares with the tabs and panels in its subtree.</summary>
/// <typeparam name="TValue">The type of the root's value.</typeparam>
internal sealed class TabsRootContext<TValue> : TabsRootContext
{
    private readonly List<PanelRegistration> panels = [];

    /// <summary>Initializes a new instance of the <see cref="TabsRootContext{TValue}"/> class.</summary>
    /// <param name="controlled">Whether the consumer owns the root's value.</param>
    /// <param name="request">Runs a user activation's request: the change callback, then the commit.</param>
    public TabsRootContext(bool controlled, Func<TValue, TabsChangeEventDetails, Task<bool>> request)
        : base(controlled)
    {
        Request = request;
    }

    /// <summary>Gets the root's value.</summary>
    /// <value>The value; meaningful only while <see cref="HasSelection"/> is <see langword="true"/> and the implicit default is not in force.</value>
    public TValue Value { get; private set; } = default!;

    // ADR 08 Amendments 2026-10-26 ("nothing selected", for a non-nullable TValue too).
    /// <summary>Gets a value indicating whether the root holds a value at all.</summary>
    /// <value><see langword="false"/> after an automatic change to nothing selected.</value>
    public bool HasSelection { get; private set; }

    // ADR 08 Amendments 2026-10-26: Base UI's implicit `defaultValue = 0`, which is not a TValue.
    /// <summary>Gets a value indicating whether the root holds the implicit default of a root without a value or a default value.</summary>
    /// <value><see langword="true"/> until the first commit of an uncontrolled root that was given neither.</value>
    public bool ImplicitDefault { get; private set; }

    /// <inheritdoc />
    public override bool HasValue => ImplicitDefault || (HasSelection && !RadioValues.IsNull(Value));

    /// <summary>Gets the root's half of a user activation.</summary>
    /// <value>A function that receives the tab's value and the details and returns whether the request was not canceled.</value>
    public Func<TValue, TabsChangeEventDetails, Task<bool>> Request { get; }

    // TAB-STA-02, TAB-API-07/12: `value === root value`; ADR 08 Amendments 2026-10-22: a null root value selects nothing.
    /// <summary>Determines whether a tab or panel with the given value is the active one.</summary>
    /// <param name="value">The tab's or panel's value.</param>
    /// <returns><see langword="true"/> when the root's value strictly equals <paramref name="value"/>, or the implicit default matches it.</returns>
    public bool IsActive(TValue value)
    {
        if (ImplicitDefault)
        {
            return TabsImplicitDefault.Matches(value);
        }

        return HasSelection && !RadioValues.IsNull(Value) && Equality.StrictEquals(Value, value);
    }

    /// <summary>Takes the root's value and notifies the parts when it changed.</summary>
    /// <param name="value">The value.</param>
    /// <param name="hasSelection">Whether the root holds a value.</param>
    /// <param name="implicitDefault">Whether the implicit default is in force.</param>
    public void SetValue(TValue value, bool hasSelection, bool implicitDefault)
    {
        if (hasSelection == HasSelection && implicitDefault == ImplicitDefault && Equality.Is(value, Value))
        {
            return;
        }

        Value = value;
        HasSelection = hasSelection;
        ImplicitDefault = implicitDefault;
        Notify();
    }

    // TAB-STA-21: a later registration for the same value replaces the earlier; an unregistration removes the entry only if
    // it still holds that panel's id. Keys compare as a JavaScript Map's do (SameValueZero: NaN finds NaN, 0 finds -0).
    /// <summary>Records the id of a panel rendered for a value.</summary>
    /// <param name="value">The panel's value.</param>
    /// <param name="panelId">The id the panel generated.</param>
    public void RegisterPanel(TValue value, string panelId)
    {
        int index = FindPanel(value);
        if (index >= 0)
        {
            if (string.Equals(panels[index].Id, panelId, StringComparison.Ordinal))
            {
                return;
            }

            panels[index] = new PanelRegistration(value, panelId);
        }
        else
        {
            panels.Add(new PanelRegistration(value, panelId));
        }

        Notify();
    }

    /// <summary>Removes a panel's registration, unless another panel with the same value took it over.</summary>
    /// <param name="value">The value the panel registered under.</param>
    /// <param name="panelId">The id the panel registered.</param>
    public void UnregisterPanel(TValue value, string panelId)
    {
        int index = FindPanel(value);
        if (index < 0 || !string.Equals(panels[index].Id, panelId, StringComparison.Ordinal))
        {
            return;
        }

        panels.RemoveAt(index);
        Notify();
    }

    // TAB-ARIA-05: the id a tab's aria-controls references.
    /// <summary>Gets the id of the panel registered for a tab's value.</summary>
    /// <param name="value">The tab's value.</param>
    /// <returns>The panel's id, or <see langword="null"/> when no panel with that value is registered.</returns>
    public string? PanelIdFor(TValue value)
    {
        int index = FindPanel(value);
        return index < 0 ? null : panels[index].Id;
    }

    /// <summary>Gets the fragment that cascades this context to <paramref name="content"/>.</summary>
    /// <param name="content">The content that receives the context.</param>
    /// <returns>The fragment.</returns>
    internal RenderFragment Cascade(RenderFragment? content) => builder =>
    {
        builder.OpenComponent<CascadingValue<TabsRootContext<TValue>>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<TabsRootContext<TValue>>.Value), this);
        builder.AddComponentParameter(2, nameof(CascadingValue<TabsRootContext<TValue>>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<TabsRootContext<TValue>>.ChildContent), content);
        builder.CloseComponent();
    };

    private int FindPanel(TValue value)
    {
        for (int i = 0; i < panels.Count; i++)
        {
            TValue key = panels[i].Value;
            if (Equality.Is(key, value) || Equality.StrictEquals(key, value))
            {
                return i;
            }
        }

        return -1;
    }

    private readonly record struct PanelRegistration(TValue Value, string Id);
}
