using Microsoft.AspNetCore.Components;

namespace Iodine.Headless;

// DRW-STR-10, DRW-STA-10..12 (drawer/root/DrawerRoot.tsx:52-148, DrawerRootContext.ts): what a Drawer root adds to the
// Dialog store for its own parts and for a nested Drawer root. Cascaded fixed by DrawerRoot around the Dialog root's
// content (ADR 16 rule 9; the ContextMenuRoot precedent); a change reaches the parts through the listeners, never through
// the cascade. Drawer nesting is the nearest DrawerRoot (Parent), independently of the dialog-family nesting the store
// keeps (DLG-STR-12). The nested swipe progress (DRW-STA-13) and the visual state (DRW-STA-15) are the `drawer` module's,
// outside the render cycle (M6 track 8 design point 1).
/// <summary>The Drawer-only state of one drawer root, beside its dialog store.</summary>
internal sealed class DrawerRootContext
{
    private readonly List<(DrawerFields Fields, Action Listener)> listeners = [];

    // Whether a nested drawer reported a positive frontmost height (isNestedDrawerOpenRef).
    private bool nestedHeightReported;
    private int keyboardProviders;

    private readonly Func<SnapPointValue?, ChangeEventDetails?, Task> requestSnapPoint;
    private readonly Action<string, PopupTriggerElement> registerTrigger;
    private readonly Action<string, PopupTriggerElement, bool> unregisterTrigger;

    /// <summary>Initializes a new instance of the <see cref="DrawerRootContext"/> class.</summary>
    /// <param name="dialog">The root's dialog store.</param>
    /// <param name="requestSnapPoint">Runs a snap point change request on the root.</param>
    /// <param name="registerTrigger">Registers an element in the root's trigger registry.</param>
    /// <param name="unregisterTrigger">Unregisters an element from the root's trigger registry.</param>
    public DrawerRootContext(
        IDialogRootContext dialog,
        Func<SnapPointValue?, ChangeEventDetails?, Task> requestSnapPoint,
        Action<string, PopupTriggerElement> registerTrigger,
        Action<string, PopupTriggerElement, bool> unregisterTrigger)
    {
        Dialog = dialog;
        this.requestSnapPoint = requestSnapPoint;
        this.registerTrigger = registerTrigger;
        this.unregisterTrigger = unregisterTrigger;
    }

    /// <summary>Gets the root's dialog store.</summary>
    public IDialogRootContext Dialog { get; }

    /// <summary>Gets the nearest enclosing drawer root, or <see langword="null"/> at the top level.</summary>
    public DrawerRootContext? Parent { get; private set; }

    /// <summary>Gets the provider the root reports its open state to, or <see langword="null"/> without one.</summary>
    public DrawerProviderContext? Provider { get; private set; }

    /// <summary>Gets the direction that dismisses the drawer.</summary>
    public SwipeDirection SwipeDirection { get; private set; } = SwipeDirection.Down;

    /// <summary>Gets the popup's element, which the viewport's drawer handle writes on, or <see langword="null"/> while none is rendered.</summary>
    public ElementReference? PopupElement { get; private set; }

    /// <summary>Gets the backdrop's element, which the viewport's drawer handle writes on, or <see langword="null"/> while none is rendered.</summary>
    public ElementReference? BackdropElement { get; private set; }

    /// <summary>Gets the active snap point, or <see langword="null"/> for none.</summary>
    public SnapPointValue? ActiveSnapPoint { get; private set; }

    /// <summary>Gets the snap points, or <see langword="null"/> when none are configured.</summary>
    public IReadOnlyList<SnapPointValue>? SnapPoints { get; private set; }

    /// <summary>Gets a value indicating whether a release advances at most one snap point.</summary>
    public bool SnapToSequentialPoints { get; private set; }

    /// <summary>Gets the height of the root's own popup in pixels, as last reported; 0 while it is not measured.</summary>
    public double PopupHeight { get; private set; }

    /// <summary>Gets the height of the frontmost popup of the root's stack in pixels; 0 before any measurement.</summary>
    public double FrontmostHeight { get; private set; }

    /// <summary>Gets a value indicating whether a nested drawer is open or animating out, as it last reported.</summary>
    public bool HasNestedDrawer { get; private set; }

    /// <summary>Gets a value indicating whether a nested drawer is being swiped, as it last reported.</summary>
    public bool NestedSwiping { get; private set; }

    // M6 track 8c (issue 279): DRW-STR-09, API-11. The keyboard role rides the Viewport's handle (design point 1 (a) of
    // 2026-11-18), which acts only while a provider is registered.
    /// <summary>Gets a value indicating whether a <see cref="DrawerVirtualKeyboardProvider"/> is under the root.</summary>
    public bool HasKeyboardProvider => keyboardProviders > 0;

    /// <summary>Registers a <see cref="DrawerVirtualKeyboardProvider"/> under the root.</summary>
    /// <returns>The registration, whose disposal removes the provider.</returns>
    public IDisposable RegisterKeyboardProvider()
    {
        keyboardProviders++;
        if (keyboardProviders == 1)
        {
            Notify(DrawerFields.KeyboardProvider);
        }

        return new KeyboardProviderRegistration(this);
    }

    /// <summary>Sets the enclosing drawer root and provider, read from the root's cascades.</summary>
    /// <param name="parent">The nearest enclosing drawer root.</param>
    /// <param name="provider">The nearest provider.</param>
    public void SetAncestors(DrawerRootContext? parent, DrawerProviderContext? provider)
    {
        Parent = parent;
        Provider = provider;
    }

    // DRW-STA-02, ADR 08 Amendments 2026-11-18: the snap points are compared by sequence, so an equal list in a new instance
    // notifies nothing (and, with the `drawer` module, sends no `update`).
    /// <summary>Records the root's snap settings and notifies the parts when any changed.</summary>
    /// <param name="active">The active snap point.</param>
    /// <param name="snapPoints">The snap points.</param>
    /// <param name="sequential">The root's <c>SnapToSequentialPoints</c>.</param>
    public void SetSnapSettings(SnapPointValue? active, IReadOnlyList<SnapPointValue>? snapPoints, bool sequential)
    {
        bool changed = !Nullable.Equals(ActiveSnapPoint, active) || SnapToSequentialPoints != sequential;
        if (!SameSequence(SnapPoints, snapPoints))
        {
            SnapPoints = snapPoints;
            changed = true;
        }

        ActiveSnapPoint = active;
        SnapToSequentialPoints = sequential;
        if (changed)
        {
            Notify(DrawerFields.Settings);
        }
    }

    /// <summary>Records the root's swipe direction and notifies the parts when it changed.</summary>
    /// <param name="direction">The direction that dismisses the drawer.</param>
    public void SetSwipeDirection(SwipeDirection direction)
    {
        if (SwipeDirection != direction)
        {
            SwipeDirection = direction;
            Notify(DrawerFields.Settings);
        }
    }

    // M6 track 8b (issue 279): the Popup and the Backdrop hand their elements to the Viewport's drawer handle, which
    // writes their swipe attributes and styles (DrawerViewport.tsx reads popupRef and backdropRef).
    /// <summary>Records the popup's element and notifies the parts when it changed.</summary>
    /// <param name="element">The element, or <see langword="null"/> when the popup no longer renders.</param>
    public void SetPopupElement(ElementReference? element)
    {
        if (!string.Equals(PopupElement?.Id, element?.Id, StringComparison.Ordinal))
        {
            PopupElement = element;
            Notify(DrawerFields.Elements);
        }
    }

    /// <summary>Records the backdrop's element and notifies the parts when it changed.</summary>
    /// <param name="element">The element, or <see langword="null"/> when the backdrop no longer renders.</param>
    public void SetBackdropElement(ElementReference? element)
    {
        if (!string.Equals(BackdropElement?.Id, element?.Id, StringComparison.Ordinal))
        {
            BackdropElement = element;
            Notify(DrawerFields.Elements);
        }
    }

    // DRW-API-10, PSH-STA-26: a swipe area registers in the root's trigger registry, without the trigger data a trigger part
    // applies (Base UI's useTriggerRegistration alone).
    /// <summary>Registers an element in the root's trigger registry.</summary>
    /// <param name="id">The element's id.</param>
    /// <param name="element">The registration.</param>
    public void RegisterTrigger(string id, PopupTriggerElement element) => registerTrigger(id, element);

    /// <summary>Unregisters an element from the root's trigger registry, while the registry still maps the id to it.</summary>
    /// <param name="id">The element's id.</param>
    /// <param name="element">The registration.</param>
    /// <param name="disposing">Whether the registering part is being disposed.</param>
    public void UnregisterTrigger(string id, PopupTriggerElement element, bool disposing) => unregisterTrigger(id, element, disposing);

    // DRW-STA-03: the module's snap-point reports and the tests' probe request through here.
    /// <summary>Requests a snap point change on the root.</summary>
    /// <param name="snapPoint">The requested snap point, or <see langword="null"/> for none.</param>
    /// <param name="details">The details, or <see langword="null"/> for the reason <see cref="EventReason.None"/> with no event.</param>
    /// <returns>A task that completes when the request has run.</returns>
    public Task RequestSnapPointAsync(SnapPointValue? snapPoint, ChangeEventDetails? details) => requestSnapPoint(snapPoint, details);

    // DRW-STA-10/11 (DrawerRoot.tsx:115-121): the own height replaces the frontmost height while no nested drawer reported one.
    /// <summary>Records the root's own popup height.</summary>
    /// <param name="height">The measured height in pixels, borders included; 0 once the popup unmounts.</param>
    public void ReportPopupHeight(double height)
    {
        DrawerFields changed = DrawerFields.None;
        if (PopupHeight != height)
        {
            PopupHeight = height;
            changed |= DrawerFields.PopupHeight;
        }

        if (!nestedHeightReported && height > 0 && FrontmostHeight != height)
        {
            FrontmostHeight = height;
            changed |= DrawerFields.FrontmostHeight;
        }

        Notify(changed);
    }

    // DRW-STA-11 (DrawerRoot.tsx:123-134).
    /// <summary>Records the frontmost height a nested drawer reports.</summary>
    /// <param name="height">The nested drawer's frontmost height in pixels; 0 when it closes or unmounts.</param>
    public void ReportNestedFrontmostHeight(double height)
    {
        if (height > 0)
        {
            nestedHeightReported = true;
            SetFrontmostHeight(height);
            return;
        }

        nestedHeightReported = false;
        if (PopupHeight > 0)
        {
            SetFrontmostHeight(PopupHeight);
        }
    }

    // DRW-STA-12 (DrawerRoot.tsx:136-138).
    /// <summary>Records whether a nested drawer is present: open, or animating out.</summary>
    /// <param name="present">The nested drawer's presence.</param>
    public void ReportNestedPresence(bool present)
    {
        if (HasNestedDrawer != present)
        {
            HasNestedDrawer = present;
            Notify(DrawerFields.HasNestedDrawer);
        }
    }

    // DRW-STA-12 (DrawerRoot.tsx:145-148): forwarded unchanged to the grandparent.
    /// <summary>Records whether a nested drawer is being swiped, and forwards it to the enclosing drawer.</summary>
    /// <param name="swiping">The nested drawer's swiping state.</param>
    public void ReportNestedSwiping(bool swiping)
    {
        if (NestedSwiping != swiping)
        {
            NestedSwiping = swiping;
            Notify(DrawerFields.NestedSwiping);
        }

        Parent?.ReportNestedSwiping(swiping);
    }

    /// <summary>Adds a listener for a change of the selected fields.</summary>
    /// <param name="fields">The fields the listener selects.</param>
    /// <param name="listener">The listener.</param>
    /// <returns>The registration; disposing it removes the listener.</returns>
    public IDisposable Subscribe(DrawerFields fields, Action listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        (DrawerFields, Action) entry = (fields, listener);
        listeners.Add(entry);
        return new Registration(this, entry);
    }

    /// <summary>Gets the fragment that cascades this context to <paramref name="content"/>.</summary>
    /// <param name="content">The content that receives the context.</param>
    /// <returns>The fragment.</returns>
    internal RenderFragment Cascade(RenderFragment? content) => builder =>
    {
        builder.OpenComponent<CascadingValue<DrawerRootContext>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<DrawerRootContext>.Value), this);
        builder.AddComponentParameter(2, nameof(CascadingValue<DrawerRootContext>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<DrawerRootContext>.ChildContent), content);
        builder.CloseComponent();
    };

    /// <summary>Notifies the listeners of the changed fields.</summary>
    /// <param name="changed">The fields that changed.</param>
    internal void Notify(DrawerFields changed)
    {
        if (changed == DrawerFields.None)
        {
            return;
        }

        // A listener may remove itself or another: iterating backwards with a bounds check never skips one still there.
        for (int i = listeners.Count - 1; i >= 0; i--)
        {
            if (i < listeners.Count && (listeners[i].Fields & changed) != 0)
            {
                listeners[i].Listener();
            }
        }
    }

    private static bool SameSequence(IReadOnlyList<SnapPointValue>? left, IReadOnlyList<SnapPointValue>? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        int leftCount = left?.Count ?? 0;
        if (leftCount != (right?.Count ?? 0) || left is null || right is null)
        {
            return leftCount == 0 && (right?.Count ?? 0) == 0;
        }

        for (int i = 0; i < leftCount; i++)
        {
            if (left[i] != right[i])
            {
                return false;
            }
        }

        return true;
    }

    private void SetFrontmostHeight(double height)
    {
        if (FrontmostHeight != height)
        {
            FrontmostHeight = height;
            Notify(DrawerFields.FrontmostHeight);
        }
    }

    private sealed class KeyboardProviderRegistration(DrawerRootContext owner) : IDisposable
    {
        private bool disposed;

        /// <inheritdoc />
        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            owner.keyboardProviders--;
            if (owner.keyboardProviders == 0)
            {
                owner.Notify(DrawerFields.KeyboardProvider);
            }
        }
    }

    private sealed class Registration(DrawerRootContext owner, (DrawerFields, Action) entry) : IDisposable
    {
        private bool disposed;

        /// <inheritdoc />
        public void Dispose()
        {
            if (!disposed)
            {
                disposed = true;
                owner.listeners.Remove(entry);
            }
        }
    }
}

/// <summary>Specifies the fields of a drawer root that a part selects.</summary>
[Flags]
internal enum DrawerFields
{
    /// <summary>No field.</summary>
    None = 0,

    /// <summary>The own popup height.</summary>
    PopupHeight = 1 << 0,

    /// <summary>The frontmost height.</summary>
    FrontmostHeight = 1 << 1,

    /// <summary>Whether a nested drawer is present.</summary>
    HasNestedDrawer = 1 << 2,

    /// <summary>Whether a nested drawer is being swiped.</summary>
    NestedSwiping = 1 << 3,

    /// <summary>The swipe direction, the snap points and the active snap point.</summary>
    Settings = 1 << 4,

    /// <summary>The popup's and the backdrop's elements.</summary>
    Elements = 1 << 5,

    /// <summary>Whether a VirtualKeyboardProvider is under the root.</summary>
    KeyboardProvider = 1 << 6,
}
