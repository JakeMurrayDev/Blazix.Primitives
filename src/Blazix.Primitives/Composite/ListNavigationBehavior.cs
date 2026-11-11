using Blazix.Primitives.Interop;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// List navigation's C# half (M5 track 1, issue 159; ADR 04 Amendments 2026-11-03 (1)-(3), chosen by the user;
// composite-and-list-navigation.md §4.2.4, §4.6-§4.9, §4.12). Store-agnostic, as InteractionsSessionBehavior is: it takes
// the session token and a request delegate, so Menu's PopupStore and track 2's open lifecycle (Select, Combobox) both
// wire it (ADR 05 Amendments 2026-11-03). One per list, composed into the family's Root, never a component (ADR 16 rule
// 10); its handles are composite roots in list mode (design point 1's C# refinement (r1) of 2026-11-04), which find it
// through the cascaded ListNavigationContext.
//
// - The options (CMP §6.8, §6.9; fact sheet A §3's union) are set by the family on every parameter set and compared
//   field by field; the record is built only after a change, and the same instance goes to every handle, which compares
//   it by reference (ADR 16 rule 7). The Root calls FlushAsync from its OnAfterRenderAsync: a handle whose own
//   parameters did not change is not rendered again, so only the Root's after-render reaches every handle.
// - JavaScript writes the highlight (the items' tabindex and data-highlighted, aria-activedescendant); C# mirrors it from
//   the report, a mirrored field (ADR 01's RND-STA-01 row), and asks for its own highlights by revision: a report that
//   answers an older revision is stale and ignored (CMP-STA-32).
// - The list's open requests (CMP-EVT-06, EVT-07, EVT-08) run the family's sequence through the request delegate; on
//   WebAssembly the Shift+Tab close runs inside `leave` and its cancel decides the focus return (ADR 04 Amendments
//   2026-11-03 (3); Server: the module returns the focus, ADR 01's "C#-origin consumer vetoes" row).
// - A list handle attaches after the session's handle (the Root's), so the module reads the session's committed state at
//   attach (`SessionReady`): a list handle can otherwise attach first and miss an open that emitted no `openchange`.
// - The element-typed options (CMP-KEY-27, CMP-DIS-01's return element; CMP-KEY-14's relay root, Deliverables 6-7) cross
//   as indices into the references every handle attaches and updates with (ADR 04 Amendments 2026-10-27): the return
//   element first, then the relay's root, each present only when set.
/// <summary>
/// The list navigation of one popup list: the options its handles send, the mirror of the highlight the module writes,
/// C#'s highlight requests, and the routing of the module's open and close requests into the family's store.
/// </summary>
internal sealed class ListNavigationBehavior : IDisposable
{
    private const string Vertical = "vertical";

    private readonly Func<OpenChangeRequest, Task> requestOpenChange;
    private readonly Func<Exception, Task> dispatchException;
    private readonly List<CompositeRootBehavior> handles = [];
    private CompositeListOptions? options;
    private CompositeHighlightRequest? highlightRequest;
    private bool enabled = true;
    private bool loopFocus;
    private string? orientation = Vertical;
    private string? triggerOrientation;
    private bool rtl;
    private bool isVirtual;
    private bool nested;
    private string? parentOrientation;
    private bool? focusItemOnOpen;
    private bool allowEscape;
    private bool openOnArrowKeyDown = true;
    private bool focusItemOnHover = true;
    private bool resetOnPointerLeave = true;
    private bool retainHighlightWhileClosing;
    private IReadOnlyList<int>? disabledIndices;
    private bool deriveDisabledIndices;
    private int? selectedIndex;
    private bool grid;
    private bool keepOpenOnCrossOrientationKey;
    private int? cols;
    private int? itemCount;
    private int typeaheadResetMs;
    private string? typeaheadSkip;
    private IReadOnlyList<string?>? typeaheadLabels;
    private bool typeahead;
    private ElementReference? nestedReturnFocusTarget;
    private ElementReference? relayTarget;
    private TwinReferences? references;
    private bool disposed;

    /// <summary>Occurs when the mirrored highlight changed, once per accepted report, with the report.</summary>
    public event Action<CompositeListHighlight>? HighlightChanged;

    /// <summary>Occurs when the typeahead of the closed list matched an item, for the family's value request.</summary>
    public event Action<CompositeListMatch>? Matched;

    /// <summary>Initializes a new instance of the <see cref="ListNavigationBehavior"/> class.</summary>
    /// <param name="sessionToken">The token of the popup's interactions session, which every handle joins.</param>
    /// <param name="sessionReady">A task that completes when the session's handle is attached.</param>
    /// <param name="requestOpenChange">Runs the family's open-change sequence for a request.</param>
    /// <param name="dispatchException">Routes an exception of a request that ran in a decision to the part.</param>
    public ListNavigationBehavior(
        string sessionToken,
        Task sessionReady,
        Func<OpenChangeRequest, Task> requestOpenChange,
        Func<Exception, Task> dispatchException)
    {
        SessionToken = sessionToken;
        SessionReady = sessionReady;
        this.requestOpenChange = requestOpenChange;
        this.dispatchException = dispatchException;
        Context = new ListNavigationContext(this);
    }

    /// <summary>Gets the token of the popup's interactions session.</summary>
    /// <value>The token every handle of the list joins.</value>
    public string SessionToken { get; }

    /// <summary>Gets a task that completes when the session's handle is attached.</summary>
    /// <value>The session behavior's attach, which every handle of the list awaits before its own.</value>
    public Task SessionReady { get; }

    /// <summary>Gets the context the Root cascades to the list's handles.</summary>
    /// <value>The context, cascaded with <c>IsFixed</c>.</value>
    public ListNavigationContext Context { get; }

    /// <summary>Gets or sets a value indicating whether list navigation is enabled.</summary>
    /// <value><see langword="true"/>, the default; <see langword="false"/> turns every handle's navigation off.</value>
    public bool Enabled { get => enabled; set => Set(ref enabled, value); }

    /// <summary>Gets or sets a value indicating whether a step past either end wraps to the other end.</summary>
    /// <value><see langword="true"/> to wrap; the default is <see langword="false"/>.</value>
    public bool LoopFocus { get => loopFocus; set => Set(ref loopFocus, value); }

    /// <summary>Gets or sets the arrow keys that move the highlight.</summary>
    /// <value><c>vertical</c>, the default, <c>horizontal</c> or <c>both</c>.</value>
    public string Orientation { get => orientation ?? Vertical; set => Set(ref orientation, value); }

    /// <summary>Gets or sets the arrow keys that open the list from its trigger.</summary>
    /// <value><c>horizontal</c>, <c>vertical</c> or <c>both</c>; <see langword="null"/>, the default, for <see cref="Orientation"/>.</value>
    public string? TriggerOrientation { get => triggerOrientation; set => Set(ref triggerOrientation, value); }

    /// <summary>Gets or sets a value indicating whether the reading direction is right to left.</summary>
    /// <value><see langword="true"/> for right to left; the default is <see langword="false"/>.</value>
    public bool Rtl { get => rtl; set => Set(ref rtl, value); }

    /// <summary>Gets or sets a value indicating whether the highlight moves without moving the focus.</summary>
    /// <value><see langword="true"/> for virtual focus; the default is <see langword="false"/>.</value>
    public bool Virtual { get => isVirtual; set => Set(ref isVirtual, value); }

    /// <summary>Gets or sets a value indicating whether the list is nested in another list.</summary>
    /// <value><see langword="true"/> for a submenu; the default is <see langword="false"/>.</value>
    public bool Nested { get => nested; set => Set(ref nested, value); }

    /// <summary>Gets or sets the orientation of the parent list.</summary>
    /// <value>The orientation, or <see langword="null"/>, the default, to read the parent popup's.</value>
    public string? ParentOrientation { get => parentOrientation; set => Set(ref parentOrientation, value); }

    /// <summary>Gets or sets a value indicating whether an item is highlighted when the list opens.</summary>
    /// <value><see langword="true"/> or <see langword="false"/>; <see langword="null"/>, the default, for a keyboard open only.</value>
    public bool? FocusItemOnOpen { get => focusItemOnOpen; set => Set(ref focusItemOnOpen, value); }

    /// <summary>Gets or sets a value indicating whether a step past either end clears the highlight under virtual focus.</summary>
    /// <value><see langword="true"/> to allow it; the default is <see langword="false"/>.</value>
    public bool AllowEscape { get => allowEscape; set => Set(ref allowEscape, value); }

    /// <summary>Gets or sets a value indicating whether an arrow key on the closed trigger opens the list.</summary>
    /// <value><see langword="true"/>, the default, to open it.</value>
    public bool OpenOnArrowKeyDown { get => openOnArrowKeyDown; set => Set(ref openOnArrowKeyDown, value); }

    /// <summary>Gets or sets a value indicating whether moving the pointer over an item highlights it.</summary>
    /// <value><see langword="true"/>, the default, to highlight it.</value>
    public bool FocusItemOnHover { get => focusItemOnHover; set => Set(ref focusItemOnHover, value); }

    /// <summary>Gets or sets a value indicating whether the pointer leaving an item clears the highlight.</summary>
    /// <value><see langword="true"/>, the default, to clear it.</value>
    public bool ResetOnPointerLeave { get => resetOnPointerLeave; set => Set(ref resetOnPointerLeave, value); }

    /// <summary>Gets or sets a value indicating whether the highlight stays on its item while the list closes.</summary>
    /// <value><see langword="true"/> to keep it until the list is hidden; <see langword="false"/>, the default, to clear it at the close.</value>
    public bool RetainHighlightWhileClosing { get => retainHighlightWhileClosing; set => Set(ref retainHighlightWhileClosing, value); }

    /// <summary>Gets or sets the indices of the items navigation skips.</summary>
    /// <value>The indices, an empty list for none, or <see langword="null"/>, the default, to read the document.</value>
    public IReadOnlyList<int>? DisabledIndices
    {
        get => disabledIndices;
        set
        {
            if (!SameIndices(disabledIndices, value))
            {
                disabledIndices = value is null ? null : [.. value];
                options = null;
            }
        }
    }

    /// <summary>Gets or sets a value indicating whether the indices navigation skips are derived from the items' flags.</summary>
    /// <value><see langword="true"/> to derive them; the default is <see langword="false"/>.</value>
    public bool DeriveDisabledIndices { get => deriveDisabledIndices; set => Set(ref deriveDisabledIndices, value); }

    /// <summary>Gets or sets the index of the selected item, which the list opens on.</summary>
    /// <value>The index, or <see langword="null"/>, the default, when no item is selected.</value>
    public int? SelectedIndex { get => selectedIndex; set => Set(ref selectedIndex, value); }

    /// <summary>Gets or sets a value indicating whether the items are a grid.</summary>
    /// <value><see langword="true"/> for a grid; the default is <see langword="false"/>.</value>
    public bool Grid { get => grid; set => Set(ref grid, value); }

    /// <summary>Gets or sets a value indicating whether a nested list that is its menu's root stays open on the nested close key.</summary>
    /// <value><see langword="true"/> for a context menu's root menu; the default is <see langword="false"/>.</value>
    public bool KeepOpenOnCrossOrientationKey { get => keepOpenOnCrossOrientationKey; set => Set(ref keepOpenOnCrossOrientationKey, value); }

    /// <summary>Gets or sets the number of columns of a grid without rows.</summary>
    /// <value>The count, or <see langword="null"/>, the default, for 2.</value>
    public int? Cols { get => cols; set => Set(ref cols, value); }

    /// <summary>Gets or sets the number of items of a windowed list, of which only some are rendered.</summary>
    /// <value>The count, or <see langword="null"/>, the default, for the registered items.</value>
    public int? ItemCount { get => itemCount; set => Set(ref itemCount, value); }

    /// <summary>Gets or sets the element a nested list returns the focus to when its close key closes it.</summary>
    /// <value>The element, such as the parent menu's input; or <see langword="null"/>, the default, for the reference.</value>
    public ElementReference? NestedReturnFocusTarget { get => nestedReturnFocusTarget; set => Set(ref nestedReturnFocusTarget, value); }

    /// <summary>Gets or sets the composite root a keydown the list does not stop is relayed to.</summary>
    /// <value>The root's element, such as a menubar's; or <see langword="null"/>, the default, to relay nothing.</value>
    public ElementReference? RelayTarget { get => relayTarget; set => Set(ref relayTarget, value); }

    /// <summary>Gets the highlighted index the module last reported.</summary>
    /// <value>The index, or <see langword="null"/> while no item is highlighted.</value>
    public int? HighlightedIndex { get; private set; }

    /// <summary>Gets the id of the highlighted item's element the module last reported.</summary>
    /// <value>The id, or <see langword="null"/> while no item is highlighted.</value>
    public string? HighlightedItemId { get; private set; }

    /// <summary>Gets the options every handle of the list sends.</summary>
    /// <value>The record, built again only after an option changed.</value>
    internal CompositeListOptions Options => options ??= Build();

    /// <summary>Gets the elements the options name by index, sent with every handle's <c>attach</c> and <c>update</c>.</summary>
    /// <value>The references built with <see cref="Options"/>, or <see langword="null"/> when no option names an element.</value>
    internal TwinReferences? References
    {
        get
        {
            options ??= Build();
            return references;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        disposed = true;
        handles.Clear();
        HighlightChanged = null;
        Matched = null;
    }

    /// <summary>Sets the typeahead of the list, or turns it off.</summary>
    /// <param name="enabled">Whether the list has a typeahead.</param>
    /// <param name="resetMs">The time in milliseconds after the last key at which a new search starts.</param>
    /// <param name="skip">The items a search skips: <c>element</c>, <c>attribute</c> or <see langword="null"/> for none.</param>
    /// <param name="labels">The labels C# supplies by index, or <see langword="null"/> for the registered items' labels.</param>
    public void SetTypeahead(bool enabled, int resetMs = 750, string? skip = null, IReadOnlyList<string?>? labels = null)
    {
        if (typeahead == enabled
            && typeaheadResetMs == resetMs
            && string.Equals(typeaheadSkip, skip, StringComparison.Ordinal)
            && SameLabels(typeaheadLabels, labels))
        {
            return;
        }

        typeahead = enabled;
        typeaheadResetMs = resetMs;
        typeaheadSkip = skip;
        typeaheadLabels = labels is null ? null : [.. labels];
        options = null;
    }

    /// <summary>
    /// Asks the module for a highlight C# decided, such as an automatic highlight, a clear or the selected item, and
    /// sends it to the list's handles at once, so it reaches the module before the render batch.
    /// </summary>
    /// <param name="index">The index to highlight, or <see langword="null"/> to clear the highlight.</param>
    /// <returns>A task that completes when the handles have the request.</returns>
    public Task RequestHighlightAsync(int? index)
    {
        highlightRequest = new CompositeHighlightRequest(index, (highlightRequest?.Revision ?? 0) + 1);
        options = null;
        return FlushAsync();
    }

    /// <summary>Sends the list's options to every attached handle that has not sent them.</summary>
    /// <returns>A task that completes when the handles have the options.</returns>
    public Task FlushAsync()
    {
        if (disposed || handles.Count == 0)
        {
            return Task.CompletedTask;
        }

        if (handles.Count == 1)
        {
            return handles[0].FlushAsync();
        }

        Task[] flushes = new Task[handles.Count];
        for (int i = 0; i < handles.Count; i++)
        {
            flushes[i] = handles[i].FlushAsync();
        }

        return Task.WhenAll(flushes);
    }

    /// <summary>Adds a handle of the list.</summary>
    /// <param name="handle">The composite root in list mode.</param>
    internal void Register(CompositeRootBehavior handle)
    {
        if (!handles.Contains(handle))
        {
            handles.Add(handle);
        }
    }

    /// <summary>Removes a handle of the list.</summary>
    /// <param name="handle">The composite root in list mode.</param>
    internal void Unregister(CompositeRootBehavior handle) => handles.Remove(handle);

    // CMP-STA-31, CMP-EVT-04 as a mirror; CMP-STA-32: a report that answers an older request than the last one sent is
    // stale, so C#'s own request is not overwritten by a report made before the module had it.
    /// <summary>Receives a handle's highlight report.</summary>
    /// <param name="report">The report.</param>
    /// <returns><see langword="true"/> when the mirror took the report.</returns>
    internal bool OnHighlight(CompositeListHighlight report)
    {
        if (disposed || report.Revision < (highlightRequest?.Revision ?? 0))
        {
            return false;
        }

        HighlightedIndex = report.Index;
        HighlightedItemId = report.ItemId;
        HighlightChanged?.Invoke(report);
        return true;
    }

    // CMP-EVT-06, CMP-EVT-07, CMP-EVT-08 (Server, or a close the module asked for outside `leave`).
    /// <summary>Receives a handle's open or close request.</summary>
    /// <param name="request">The request.</param>
    internal void OnOpenRequest(CompositeListOpenRequest request)
    {
        if (disposed)
        {
            return;
        }

        OpenChangeEventDetails details = new(ParseReason(request.Reason), request.Event, Trigger(request.TriggerId));
        _ = ObserveAsync(requestOpenChange(new OpenChangeRequest(request.Open, details, false, null)));
    }

    /// <summary>Receives a handle's typeahead match on the closed list.</summary>
    /// <param name="match">The match.</param>
    internal void OnMatch(CompositeListMatch match)
    {
        if (!disposed)
        {
            Matched?.Invoke(match);
        }
    }

    // The decision point `leave` (ADR 04 Amendments 2026-11-03 (3)), WebAssembly only, synchronously inside the keydown:
    // only a consumer callback that completes synchronously can cancel the close.
    /// <summary>Runs the Shift+Tab close inside the dispatch and answers whether the module returns the focus.</summary>
    /// <param name="snapshot">The <c>keydown</c>.</param>
    /// <returns><see cref="DecisionPolicy.Run"/> when the close was not canceled; <see cref="DecisionPolicy.Stop"/> when it was.</returns>
    internal DecisionPolicy DecideLeave(EventSnapshot snapshot)
    {
        OpenChangeEventDetails details = new(EventReason.FocusOut, snapshot);
        try
        {
            _ = ObserveAsync(requestOpenChange(new OpenChangeRequest(false, details, false, null)));
        }
        catch (Exception exception)
        {
            _ = dispatchException(exception);
            return DecisionPolicy.Stop;
        }

        return details.IsCanceled ? DecisionPolicy.Stop : DecisionPolicy.Run;
    }

    private static EventReason ParseReason(string literal) => literal switch
    {
        "list-navigation" => EventReason.ListNavigation,
        "focus-out" => EventReason.FocusOut,
        _ => throw new ArgumentOutOfRangeException(nameof(literal), $"Blazix: `{literal}` is not a reason the `composite` module reports."),
    };

    private static EventTrigger? Trigger(string? id) => id is null ? null : new EventTrigger(id, null);

    private static bool SameIndices(IReadOnlyList<int>? a, IReadOnlyList<int>? b)
    {
        if (a is null || b is null)
        {
            return a is null && b is null;
        }

        if (a.Count != b.Count)
        {
            return false;
        }

        for (int i = 0; i < a.Count; i++)
        {
            if (a[i] != b[i])
            {
                return false;
            }
        }

        return true;
    }

    private static bool SameLabels(IReadOnlyList<string?>? a, IReadOnlyList<string?>? b)
    {
        if (a is null || b is null)
        {
            return a is null && b is null;
        }

        if (a.Count != b.Count)
        {
            return false;
        }

        for (int i = 0; i < a.Count; i++)
        {
            if (!string.Equals(a[i], b[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private void Set(ref bool field, bool value)
    {
        if (field != value)
        {
            field = value;
            options = null;
        }
    }

    private void Set(ref bool? field, bool? value)
    {
        if (field != value)
        {
            field = value;
            options = null;
        }
    }

    private void Set(ref int? field, int? value)
    {
        if (field != value)
        {
            field = value;
            options = null;
        }
    }

    // Compared by id: an element reference is the id Blazor rendered for the element.
    private void Set(ref ElementReference? field, ElementReference? value)
    {
        if (!string.Equals(field?.Id, value?.Id, StringComparison.Ordinal))
        {
            field = value;
            options = null;
        }
    }

    private void Set(ref string? field, string? value)
    {
        if (!string.Equals(field, value, StringComparison.Ordinal))
        {
            field = value;
            options = null;
        }
    }

    // Each option is sent only when it differs from Base UI's default (useListNavigation.ts:244-267).
    private CompositeListOptions Build()
    {
        references = null;
        int? nestedReturnFocusElement = nestedReturnFocusTarget is { } returnElement ? AddReference(returnElement) : null;
        int? relayElement = relayTarget is { } relayRoot ? AddReference(relayRoot) : null;
        return Build(nestedReturnFocusElement, relayElement);
    }

    private int AddReference(ElementReference element) => (references ??= new TwinReferences()).Add(element);

    private CompositeListOptions Build(int? nestedReturnFocusElement, int? relayElement) => new()
    {
        Enabled = enabled ? null : false,
        LoopFocus = loopFocus,
        Orientation = orientation is null || string.Equals(orientation, Vertical, StringComparison.Ordinal) ? null : orientation,
        TriggerOrientation = triggerOrientation,
        Rtl = rtl,
        Virtual = isVirtual,
        Nested = nested,
        ParentOrientation = parentOrientation,
        FocusItemOnOpen = focusItemOnOpen,
        AllowEscape = allowEscape,
        OpenOnArrowKeyDown = openOnArrowKeyDown ? null : false,
        FocusItemOnHover = focusItemOnHover ? null : false,
        ResetOnPointerLeave = resetOnPointerLeave ? null : false,
        RetainHighlightWhileClosing = retainHighlightWhileClosing,
        DisabledIndices = disabledIndices,
        DeriveDisabledIndices = deriveDisabledIndices,
        SelectedIndex = selectedIndex,
        Grid = grid,
        KeepOpenOnCrossOrientationKey = keepOpenOnCrossOrientationKey,
        NestedReturnFocusElement = nestedReturnFocusElement,
        RelayElement = relayElement,
        Highlight = highlightRequest,
        Typeahead = typeahead ? new CompositeTypeaheadOptions { ResetMs = typeaheadResetMs, Skip = typeaheadSkip, Labels = typeaheadLabels } : null,
        Cols = cols,
        ItemCount = itemCount,
    };

    private async Task ObserveAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception exception) when (!task.IsCanceled)
        {
            await dispatchException(exception);
        }
    }
}
