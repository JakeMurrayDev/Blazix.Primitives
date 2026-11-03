using Blazix.Primitives.Fixtures.Client.FocusManagerFixtures;
using Blazix.Primitives.Fixtures.Client.PopupFixtures;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives.Fixtures.Client.CompositeFixtures;

/// <summary>
/// The test-only list popup at L4 (M5 track 1, issue 159, Deliverable 12), placed inside a
/// <see cref="FixturePopupPortal{TPayload}"/>: while modal and open the internal backdrop (the outside press lands on it, so
/// the page under it receives nothing), then a <see cref="FixtureListPositioner"/> (the <c>interactions</c> floating handle,
/// above the backdrop) around the M4 host's <see cref="FixturePopupPopup{TPayload}"/> (the store's popup element,
/// <c>data-testid="popup"</c>) around a <see cref="FixtureFocusManaged"/>: non-modal, the return focus to the trigger (none
/// under virtual focus), the focus-out close, and the initial focus on the list element unless the open highlights an item
/// (Base UI's <c>MenuPopup.tsx:84-96</c>: a keyboard open highlights an item, which list navigation focuses).
/// </summary>
public sealed class FixtureListPopup : ComponentBase, IDisposable
{
    private SelectorSubscription<FixturePopupStoreState<NoPayload>>? subscription;
    private StoreSelection<FixturePopupStoreState<NoPayload>, bool>? open;

    [CascadingParameter]
    private FixtureListContext? HostContext { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>Gets or sets the id of the trigger, the focus manager's reference.</summary>
    [Parameter]
    public string? TriggerId { get; set; }

    /// <summary>Gets or sets the id of the list element, which takes the initial focus when no item is highlighted.</summary>
    [Parameter]
    public string? ListId { get; set; }

    /// <summary>Gets or sets the popup element's attributes (its test id).</summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    public void Dispose() => subscription?.Dispose();

    protected override void OnInitialized()
    {
        subscription = new SelectorSubscription<FixturePopupStoreState<NoPayload>>(HostContext!.Store, () => _ = InvokeAsync(StateHasChanged));
        open = subscription.Select(PopupSelectors<FixturePopupStoreState<NoPayload>, NoPayload>.Open);
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        FixtureListContext host = HostContext!;
        bool isOpen = open!.Value;
        if (host.Modal && isOpen)
        {
            PopupInternalBackdrop.Render(builder, host.Store.Popup.FloatingId + "-backdrop", inert: false);
        }

        // A key opening a non-empty list highlights an item, which list navigation focuses. A nested list's focus manager
        // neither takes the initial focus nor returns it (Base UI's MenuOrientation.tsx: `initialFocus={!isNested}`,
        // `returnFocus={!isNested}`): the list's close key returns it.
        FixtureListRoot root = host.Root;
        bool initialFocus = root.InitialFocus && !root.Nested && !host.Virtual && (!host.OpenedByKeyboard || root.Items.Count == 0);
        builder.OpenComponent<FixtureListPositioner>(1);
        builder.AddComponentParameter(2, nameof(FixtureListPositioner.ChildContent), (RenderFragment)(positioned =>
        {
            positioned.OpenComponent<FixturePopupPopup<NoPayload>>(0);
            positioned.AddMultipleAttributes(1, AdditionalAttributes);
            positioned.AddAttribute(2, "role", "presentation");
            positioned.AddComponentParameter(3, nameof(FixturePopupPopup<NoPayload>.ChildContent), (RenderFragment)(content =>
            {
                content.OpenComponent<FixtureFocusManaged>(0);
                content.AddComponentParameter(1, nameof(FixtureFocusManaged.Open), isOpen);
                content.AddComponentParameter(2, nameof(FixtureFocusManaged.Modal), false);
                content.AddComponentParameter(3, nameof(FixtureFocusManaged.InitialFocus), initialFocus);
                content.AddComponentParameter(4, nameof(FixtureFocusManaged.InitialFocusId), root.InitialFocusOnList ? ListId : null);
                content.AddComponentParameter(5, nameof(FixtureFocusManaged.ReturnFocus), !host.Virtual && !root.Nested);
                content.AddComponentParameter(6, nameof(FixtureFocusManaged.DomReferenceId), TriggerId);
                content.AddComponentParameter(7, nameof(FixtureFocusManaged.OnFocusOutClose), EventCallback.Factory.Create<EventSnapshot>(this, OnFocusOutCloseAsync));
                content.AddComponentParameter(8, nameof(FixtureFocusManaged.ChildContent), ChildContent);
                content.AddComponentParameter(9, nameof(FixtureFocusManaged.CloseOnFocusOut), root.Dismissible);
                content.CloseComponent();
            }));
            positioned.CloseComponent();
        }));
        builder.CloseComponent();
    }

    private Task OnFocusOutCloseAsync(EventSnapshot snapshot) =>
        HostContext!.Store.RequestOpenAsync(false, new OpenChangeEventDetails(EventReason.FocusOut, snapshot));
}
