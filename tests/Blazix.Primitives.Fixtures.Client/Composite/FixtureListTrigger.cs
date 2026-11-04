using Blazix.Primitives.Fixtures.Client.PopupFixtures;
using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Fixtures.Client.CompositeFixtures;

/// <summary>
/// The test-only list trigger at L4 (M5 track 1, issue 159, Deliverable 12): the list's reference. A <c>button</c> (the
/// DOM-focus host) or, with <see cref="Input"/>, an <c>input</c> with <c>role="combobox"</c> (the virtual-focus host),
/// composing three things on one element: <see cref="PopupTriggerBehavior{TStore, TState, TPayload}"/> (the registration
/// with the popup store, <c>aria-expanded</c>, <c>aria-controls</c>), the <c>interactions</c> trigger handle (a press
/// toggles, or only opens for the input; it makes the trigger the session's reference, so a press on it is no outside
/// press) and a composite root in list mode in the <see cref="CompositeListRole.Reference"/> role (the list's keys while
/// closed, and <c>aria-activedescendant</c> under virtual focus).
/// </summary>
/// <remarks>
/// With <see cref="Item"/> the trigger is also an item of the enclosing composite root, a submenu trigger of a parent
/// list (the nested page) or a menubar's item (the relay page), with the item's <c>Highlighted</c> mirror as
/// <c>data-mirrored-highlighted</c>.
/// The input also takes the Combobox family's Enter (Base UI's <c>ComboboxInput.tsx:404-420</c>, which clicks the
/// highlighted item): a test-only listener of the fixture host (<c>blazixFixtureDom.listenComboboxEnter</c>) clicks the
/// element <c>aria-activedescendant</c> names and prevents the key, so a surrounding form is not submitted.
/// </remarks>
public sealed class FixtureListTrigger : PartBase<FixturePopupTriggerState>
{
    private static readonly StateAttributesMapping<FixturePopupTriggerState> s_mapping = PopupStateMapping.CreateTrigger<FixturePopupTriggerState>();
    private static readonly InteractionsClickOptions s_toggle = new();
    private static readonly InteractionsClickOptions s_openOnly = new() { Toggle = false };

    private readonly PopupTriggerBehavior<FixturePopupStore<NoPayload>, FixturePopupStoreState<NoPayload>, NoPayload> behavior;
    private readonly EventCallback onClick;
    private readonly EventCallback onKeyDown;
    private InteractionsTriggerBehavior? interactions;
    private CompositeRootBehavior? reference;
    private CompositeItemBehavior? item;
    private FixturePopupTriggerState state = new(false);
    private bool enterListened;
    private bool interactionsAttached;

    public FixtureListTrigger()
    {
        behavior = new(this, "FixtureListTrigger", StateHasChanged);
        onClick = CreateHandler<MouseEventArgs>("onclick", e => interactions!.HandleClickAsync(e));
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", e => reference!.HandleKeyDownAsync(e));
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    [CascadingParameter]
    private FixturePopupStore<NoPayload>? RootStore { get; set; }

    [CascadingParameter]
    private ListNavigationContext? ListContext { get; set; }

    [CascadingParameter]
    private FixtureListContext? HostContext { get; set; }

    [CascadingParameter]
    private CompositeRootContext? ParentRoot { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the trigger is also an item of the enclosing composite root: a parent list's
    /// submenu trigger or a menubar's item.
    /// </summary>
    [Parameter]
    public bool Item { get; set; }

    /// <summary>Gets or sets a value indicating whether the trigger is an <c>input</c> with <c>role="combobox"</c>.</summary>
    [Parameter]
    public bool Input { get; set; }

    /// <summary>Gets or sets the trigger's <c>aria-haspopup</c>.</summary>
    [Parameter]
    public string HasPopup { get; set; } = "menu";

    /// <summary>Gets a task that completes when every handle of the trigger is attached.</summary>
    public Task TwinsAttached => Task.WhenAll(interactions!.TwinAttached, reference!.TwinAttached);

    protected override string DefaultTagName => Input ? "input" : "button";

    protected override FixturePopupTriggerState State
    {
        get
        {
            if (state.Open != behavior.IsOpenedByTrigger)
            {
                state = new FixturePopupTriggerState(behavior.IsOpenedByTrigger);
            }

            return state;
        }
    }

    protected override StateAttributesMapping<FixturePopupTriggerState>? StateAttributesMapping => s_mapping;

    protected override bool UsesGeneratedId => true;

    protected override void OnInitialized()
    {
        interactions = new InteractionsTriggerBehavior(JSRuntime, request => HostContext!.RequestAsync(request), DispatchExceptionAsync, onClick);
        reference = new CompositeRootBehavior(JSRuntime, IdGenerator, StateHasChanged, DispatchExceptionAsync, onKeyDown);
        if (Item)
        {
            item = new CompositeItemBehavior(ParentRoot, nameof(FixtureListTrigger), onKeyDown, StateHasChanged);
        }

        base.OnInitialized();
    }

    protected override void OnParametersSet()
    {
        behavior.Apply(RootStore, null, Id, Element, false, default, "Blazix: `FixtureListTrigger` must be used within a `FixtureListRoot` component.");
        reference!.SetList(ListContext, CompositeListRole.Reference);
        reference.SetConsumerAttributes(AdditionalAttributes);
        item?.SetConsumerAttributes(AdditionalAttributes);
        item?.SetRegistration(Id, AdditionalAttributes);
        interactions!.SetConsumerAttributes(AdditionalAttributes);
        interactions.SessionToken = behavior.Store.SessionToken;
        InteractionsClickOptions click = Input ? s_openOnly : s_toggle;
        if (!string.Equals(interactions.Options.TriggerId, Id, StringComparison.Ordinal) || !ReferenceEquals(interactions.Options.Click, click))
        {
            interactions.Options = new InteractionsTriggerOptions { TriggerId = Id, Click = click };
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await behavior.OnAfterRenderAsync(Element);
        Task interactionsSync = interactions!.AfterRenderAsync(Enabled, Element, Id);
        Task referenceSync = reference!.AfterRenderAsync(Enabled, Element, Id);
        await interactionsSync;

        // The attach-order titles press the trigger while js/composite.js is held back, once the interactions handle is in.
        if (!interactionsAttached && interactions.TwinAttached.IsCompleted)
        {
            interactionsAttached = true;
            StateHasChanged();
        }

        await referenceSync;
        if (item is not null)
        {
            await item.AfterRenderAsync(Element);
        }

        if (Input && !enterListened && Element is { } element)
        {
            enterListened = true;
            try
            {
                await JSRuntime.InvokeVoidAsync("blazixFixtureDom.listenComboboxEnter", element);
            }
            catch (JSDisconnectedException)
            {
                // The circuit went while the page was set up.
                return;
            }
        }

        if (interactions.TwinAttached.IsCompleted && reference.TwinAttached.IsCompleted)
        {
            HostContext?.CompleteTriggerAttach();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            behavior.Dispose();
            reference?.Dispose();
            item?.Dispose();
            if (interactions is not null)
            {
                _ = interactions.DisposeAsync().AsTask();
            }
        }

        base.Dispose(disposing);
    }

    protected override void AddPartAttributes(AttributeList attributes)
    {
        if (Input)
        {
            attributes.Set("type", "text");
            attributes.Set("role", "combobox");
            attributes.Set("autocomplete", "off");
        }
        else
        {
            attributes.Set("type", "button");
        }

        attributes.Set(PopupMarkerAttributes.ClickTrigger, string.Empty);
        behavior.AddAttributes(attributes, ariaExpanded: true, ariaHasPopup: HasPopup);
        attributes.Set("onclick", onClick);
        interactions!.AddAttributes(attributes);
        if (item is not null)
        {
            item.AddAttributes(attributes);
            if (item.Highlighted)
            {
                attributes.Set("data-mirrored-highlighted", string.Empty);
            }
        }

        reference!.AddAttributes(attributes, Id);
        if (interactionsAttached)
        {
            attributes.Set("data-interactions-attached", string.Empty);
        }
    }

    protected override bool ForwardsConsumerHandler(string eventName) =>
        interactions!.ForwardsConsumerHandler(eventName) && reference!.ForwardsConsumerHandler(eventName)
        && (item is null || item.ForwardsConsumerHandler(eventName));
}
