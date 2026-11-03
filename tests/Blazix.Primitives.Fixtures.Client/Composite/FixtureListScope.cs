using Blazix.Primitives.Fixtures.Client.PopupFixtures;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives.Fixtures.Client.CompositeFixtures;

/// <summary>
/// What the list host's parts share: the popup store, the list, the selectable value and how the popup was last opened.
/// </summary>
internal sealed class FixtureListContext
{
    private readonly TaskCompletionSource triggerAttached = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public FixturePopupStore<NoPayload> Store { get; set; } = null!;

    public ListNavigationBehavior List { get; set; } = null!;

    public FixtureListRoot Root { get; set; } = null!;

    public string? Value { get; set; }

    /// <summary>Gets or sets a value indicating whether the last open came from a key, so no element takes the initial focus.</summary>
    public bool OpenedByKeyboard { get; set; }

    public Task TriggerAttached => triggerAttached.Task;

    public bool Selectable => Root.Selectable;

    public bool Virtual => Root.Virtual;

    public bool Modal => Root.Modal;

    public void CompleteTriggerAttach() => triggerAttached.TrySetResult();

    /// <summary>Requests an open change on the popup store, recording whether a key opened it.</summary>
    /// <param name="request">The request.</param>
    /// <returns>A task that completes when the store has answered.</returns>
    public Task RequestAsync(OpenChangeRequest request)
    {
        if (request.Open)
        {
            OpenedByKeyboard = request.InteractionType is InteractionType.Keyboard
                || request.Details.Reason == EventReason.ListNavigation
                || (request.Details.Event is { } snapshot && snapshot.Detail == 0 && request.Details.Reason == EventReason.TriggerPress);
        }

        return Store.RequestOpenAsync(request.Open, request.Details);
    }

    /// <summary>An item's activation: the value while selectable, then the close (Base UI's item press).</summary>
    /// <param name="label">The item's label.</param>
    /// <param name="snapshot">The click.</param>
    /// <returns>A task that completes when the close is answered.</returns>
    public async Task ActivateAsync(string label, EventSnapshot snapshot)
    {
        if (Selectable)
        {
            Value = label;
            await Root.OnValueChange.InvokeAsync(label);
        }

        await Store.RequestOpenAsync(false, new OpenChangeEventDetails(EventReason.ItemPress, snapshot));
    }
}

/// <summary>
/// The list host's scope inside the popup root: it composes <see cref="ListNavigationBehavior"/> over the cascaded popup
/// store (its session token and its request path), sets the root's options on every parameter set, cascades the list and
/// the host's context, and flushes the list from its after-render as a family's Root does.
/// </summary>
internal sealed class FixtureListScope : ComponentBase, IDisposable
{
    private ListNavigationBehavior? list;
    private RenderFragment? cascadedContent;
    private RenderFragment? cascade;

    [CascadingParameter]
    private FixturePopupStore<NoPayload>? Store { get; set; }

    [Parameter]
    public FixtureListRoot Root { get; set; } = null!;

    [Parameter]
    public FixtureListContext Context { get; set; } = null!;

    [Parameter]
    public Task SessionReady { get; set; } = Task.CompletedTask;

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    public void Dispose() => list?.Dispose();

    protected override void OnInitialized()
    {
        Context.Store = Store!;
        Context.Root = Root;
        Context.Value = Root.DefaultValue;
        list = new ListNavigationBehavior(Store!.SessionToken!, SessionReady, Context.RequestAsync, exception => throw exception);
        Context.List = list;
        list.HighlightChanged += report => _ = Root.OnHighlightChange.InvokeAsync(report.Index);
    }

    protected override void OnParametersSet()
    {
        FixtureListRoot root = Root;
        list!.Enabled = root.Enabled;
        list.LoopFocus = root.LoopFocus;
        list.Orientation = root.Orientation;
        list.TriggerOrientation = root.TriggerOrientation;
        list.Rtl = root.Rtl;
        list.Virtual = root.Virtual;
        list.Nested = root.Nested;
        list.ParentOrientation = root.ParentOrientation;
        list.FocusItemOnOpen = root.FocusItemOnOpen;
        list.AllowEscape = root.AllowEscape;
        list.OpenOnArrowKeyDown = root.OpenOnArrowKeyDown;
        list.FocusItemOnHover = root.FocusItemOnHover;
        list.ResetOnPointerLeave = root.ResetOnPointerLeave;
        list.DisabledIndices = root.DisabledIndices;
        list.DeriveDisabledIndices = root.DeriveDisabledIndices;
        list.Grid = root.Grid;
        list.KeepOpenOnCrossOrientationKey = root.KeepOpenOnCrossOrientationKey;
        list.SetTypeahead(root.Typeahead, root.TypeaheadResetMs, root.TypeaheadSkip, root.TypeaheadLabels);
        list.RelayTarget = root.RelayTarget;
        int selected = Context.Selectable && Context.Value is { } value ? IndexOf(root.Items, value) : -1;
        list.SelectedIndex = selected < 0 ? null : selected;
        if (cascade is null || !ReferenceEquals(cascadedContent, ChildContent))
        {
            cascadedContent = ChildContent;
            cascade = list.Context.Cascade(cascadedContent);
        }
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<CascadingValue<FixtureListContext>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<FixtureListContext>.Value), Context);
        builder.AddComponentParameter(2, nameof(CascadingValue<FixtureListContext>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<FixtureListContext>.ChildContent), cascade);
        builder.CloseComponent();
    }

    protected override Task OnAfterRenderAsync(bool firstRender) => list!.FlushAsync();

    private static int IndexOf(IReadOnlyList<string> items, string value)
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (string.Equals(items[i], value, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }
}
