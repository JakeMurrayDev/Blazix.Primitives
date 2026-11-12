using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// MNU §3.22 state object (filter-dropdown/input/FilterDropdownInput.tsx:55-57,141-147; MNU-STA-22). Highlighted depends on
// focus-visible and keyboard modality, both known only when the event happens: the menu-filter module writes
// data-highlighted and C# mirrors it (M5 track 12's design point 5 (a), settled by the orchestrator 2026-11-11).
/// <summary>Represents the state of a menu's filter input, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Highlighted">
/// A value indicating whether the input shows its focus ring, cleared while keyboard navigation highlights an item; its
/// <c>data-highlighted</c> attribute is written by the browser-side behavior.
/// </param>
[PartState]
public sealed partial record MenuInputState([property: MirroredField] bool Highlighted);

// menu/input/MenuInputDataAttributes.ts (MNU-DOM-21).
/// <summary>Provides the names of the data attributes a menu's filter input renders.</summary>
public static class MenuInputDataAttributes
{
    /// <summary>Represents the attribute present while the input shows its focus ring: <c>data-highlighted</c>.</summary>
    public const string Highlighted = "data-highlighted";
}

// Base UI's Menu.Input (menu/input/MenuInput.tsx, filter-dropdown/input/FilterDropdownInput.tsx); the contract is
// docs/components/menu.md §3.22 (MNU-STR-15, API-59, DOM-21, ARIA-14, STA-22, EVT-16, EVT-19, KEY-31, A11Y-02). value and
// aria-activedescendant are the browser-side behavior's after attach (the Input/Field.Control precedent of ADR 04
// Amendments 2026-10-22; ADR 04 :504).
/// <summary>
/// Represents the text input that filters a menu's items, Base UI's <c>Menu.Input</c>, rendered as an <c>input</c> with
/// the <c>searchbox</c> role.
/// </summary>
/// <remarks>It must be placed in a menu wrapped in <see cref="MenuFilterProvider"/>.</remarks>
public sealed class MenuInput : PartBase<MenuInputState>
{
    private readonly EventCallback onKeyDown;
    private CompositeRootBehavior? reference;
    private MenuInputState state = new(false);
    private MenuFilterRootContext? filter;
    private MenuFilterModuleAttachment? module;
    private MenuSubscription? subscription;
    private MenuSelection<bool>? open;
    private MenuSelection<bool>? keyboardOpen;
    private IDisposable? filterSubscription;
    private IDisposable? inputRegistration;
    private string frozenValue = "";

    /// <summary>Initializes a new instance of the <see cref="MenuInput"/> class.</summary>
    public MenuInput()
    {
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", e => reference!.HandleKeyDownAsync(e));
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    [CascadingParameter]
    private MenuRootContext? RootContext { get; set; }

    [CascadingParameter]
    private PopupPortalContext? Portal { get; set; }

    [CascadingParameter]
    private DirectionProviderContext? DirectionContext { get; set; }

    /// <summary>Gets or sets a value indicating whether the input ignores user interaction.</summary>
    /// <value>The default is <see langword="false"/>. The input is also disabled while its menu is.</value>
    [Parameter]
    public bool Disabled { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "input";

    /// <inheritdoc />
    protected override MenuInputState State => state;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    /// <summary>Gets a task that completes when the input's <c>menu-filter</c> handle is attached.</summary>
    internal Task FilterAttached => module?.Attached ?? Task.CompletedTask;

    // MNU-EDGE-04, STR-15. The handle's options follow the root's open state and the filter's query, and its reports reach
    // the provider (MNU-EVT-16) and the part's state (MNU-STA-22, M5 track 12's design point 5 (a)).
    /// <inheritdoc />
    protected override void OnInitialized()
    {
        MenuFilterRootContext current = RootContext?.Filter ?? throw MenuParts.MissingFilter("Input");
        filter = current;
        frozenValue = current.Query;
        subscription = RootContext.Subscribe(RequestRender);
        open = subscription.Select(MenuSelectors.Open);
        keyboardOpen = subscription.Select(MenuSelectors.KeyboardOpen);
        filterSubscription = current.Subscribe(RequestRender);
        inputRegistration = current.RegisterInput();
        module = new MenuFilterModuleAttachment(JSRuntime);
        module.Twin.ValueChanged += change => _ = InvokeAsync(() => current.RequestQueryAsync(
            change.Value,
            string.Equals(change.Reason, "input-clear", StringComparison.Ordinal) ? EventReason.InputClear : EventReason.InputChange,
            change.Event));
        module.Twin.HighlightedChanged += highlighted => _ = InvokeAsync(() => SetHighlighted(highlighted));

        // MenuRoot.tsx:622-638 (M5 track 12, granted by the orchestrator 2026-11-11): under virtual focus the Input holds
        // real focus and carries the list's reference handle (aria-activedescendant, the navigation keys).
        reference = new CompositeRootBehavior(JSRuntime, IdGenerator, StateHasChanged, DispatchExceptionAsync, onKeyDown);
        reference.SetList(RootContext.List, CompositeListRole.Reference);

        // Design point 6 (a): the router asks `navigate` before composite's handle; both read one verdict per keydown.
        module.Twin.NavigationDecider = reference.DecideKeyDownOnce;

        // MNU-KEY-26 (useMenuFilterKeyDown.ts:179-192): Shift+Tab in the input closes the menu with focus-out; the focus
        // manager returns focus on the close.
        module.Twin.FocusedOut += report => _ = InvokeAsync(() =>
            RootContext.RequestOpenAsync(false, new OpenChangeEventDetails(EventReason.FocusOut, report.Event)));
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        reference!.SetConsumerAttributes(AdditionalAttributes);

        // MenuFilterPopup.tsx:38-50: the popup focuses the input on open, by its id.
        filter!.SetInputId(Id);
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (Element is not { } element)
        {
            return;
        }

        filter?.SetInputElement(element);
        await PopupPortalContext.WhenMoved(Portal);
        Task referenceSync = reference!.AfterRenderAsync(Enabled, element, Id);
        await module!.SyncAsync(element, new MenuFilterOptions
        {
            Part = MenuFilterTwin.InputPart,
            MenuKey = RootContext!.SessionToken,
            Value = filter!.Query,
            ValueRevision = filter.QueryRevision,
            Open = open!.Value,
            KeyboardOpen = keyboardOpen!.Value,
            DecidesKeyDown = AdditionalAttributes?.ContainsKey("onkeydown") == true,

            // useMenuFilterKeyDown.ts:38-46: the router's table reads the menu's orientation and direction.
            Orientation = RootContext.Orientation == Orientation.Horizontal ? "horizontal" : "vertical",
            Rtl = DirectionProviderContext.Read(DirectionContext) == TextDirection.Rtl,
        });
        await referenceSync;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            subscription?.Dispose();
            filterSubscription?.Dispose();
            reference?.Dispose();
            inputRegistration?.Dispose();
            if (filter is not null && string.Equals(filter.InputId, Id, StringComparison.Ordinal))
            {
                filter.SetInputId(null);
                filter.SetInputElement(null);
            }

            if (module is not null)
            {
                _ = module.DisposeAsync().AsTask();
            }
        }

        base.Dispose(disposing);
    }

    // MNU-DOM-21 (filter-dropdown/input/FilterDropdownInput.tsx:44-53, 150-163; menu/input/MenuInput.tsx:32-65), API-59: the
    // id stays on the part's own element (issue 115); disabled is the root's or the part's. value is the committed query of
    // the first render, then frozen: the module writes later ones by revision (C# -> JS, the `checked` handover of ADR 04
    // Amendments 2026-10-22), so a render never overwrites a keystroke still on its way; aria-activedescendant and
    // data-highlighted are the browser-side behaviors'.
    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        attributes.Set("type", "text");
        attributes.Set("role", "searchbox");
        attributes.Set("inputmode", "search");
        attributes.Set("autocomplete", "off");
        attributes.Set("spellcheck", "false");
        attributes.Set("autocorrect", "off");
        attributes.Set("autocapitalize", "none");
        if (filter!.ListId is { } listId)
        {
            attributes.Set("aria-controls", listId);
        }

        if (Disabled || RootContext!.State.Disabled)
        {
            attributes.Set("disabled", true);
        }

        attributes.Set("value", frozenValue);
        reference!.AddAttributes(attributes, Id);
    }

    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName) => reference!.ForwardsConsumerHandler(eventName);

    private void RequestRender() => _ = InvokeAsync(StateHasChanged);

    private void SetHighlighted(bool highlighted)
    {
        if (state.Highlighted != highlighted)
        {
            state = new MenuInputState(highlighted);
            StateHasChanged();
        }
    }
}
