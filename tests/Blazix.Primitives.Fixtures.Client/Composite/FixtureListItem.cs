using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Fixtures.Client.CompositeFixtures;

/// <summary>The state of <see cref="FixtureListItem"/>: the Highlighted mirror, whose attribute the module writes.</summary>
/// <param name="Disabled">Whether the item is disabled.</param>
/// <param name="Selected">Whether the item is the list's selected value.</param>
/// <param name="Highlighted">Whether the module's highlight is on the item.</param>
[PartState]
public sealed partial record FixtureListItemState(bool Disabled, bool Selected, [property: MirroredField] bool Highlighted);

/// <summary>
/// The test-only list item at L4 (M5 track 1, issue 159, Deliverable 12): a <c>div</c> with the item's role
/// (<c>menuitem</c> or <c>option</c>) composing <see cref="CompositeItemBehavior"/> (the registration, <c>tabindex="-1"</c>
/// until the module writes it, the Highlighted mirror) and <see cref="global::Blazix.Primitives.ButtonBehavior"/> in composite, non-native mode
/// (focusable when disabled, as Base UI's items are), so the <c>button</c> module activates the item: Enter and Space
/// dispatch a click, a Space while typing is skipped (the list handle prevents it). The part's own <c>onclick</c> runs
/// after the consumer's: while enabled it activates the item through the host (the value, then the close with
/// <c>item-press</c>). While the list is selectable it renders <c>aria-selected</c>.
/// </summary>
/// <remarks><c>Render</c> and <c>RenderFunction</c> substitute the element as on every part.</remarks>
public sealed class FixtureListItem : PartBase<FixtureListItemState>
{
    private const string ArrowKeyPrefix = "Arrow";

    private readonly EventCallback onKeyDown;
    private readonly EventCallback onKeyUp;
    private readonly EventCallback onClick;
    private global::Blazix.Primitives.ButtonBehavior? button;
    private CompositeItemBehavior? item;
    private FixtureListItemState? state;

    public FixtureListItem()
    {
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", e => IsItemKey(e.Key) ? item!.HandleKeyDownAsync(e) : button!.HandleKeyDownAsync(e));
        onKeyUp = CreateHandler<KeyboardEventArgs>("onkeyup", e => button!.HandleKeyUpAsync(e));
        onClick = CreateHandler<MouseEventArgs>("onclick", OnClickAsync);
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<FixtureListItem> Logger { get; set; } = null!;

    [Inject]
    private ButtonDevelopmentLog DevelopmentLog { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    [CascadingParameter]
    private CompositeRootContext? RootContext { get; set; }

    [CascadingParameter]
    private FixtureListContext? HostContext { get; set; }

    /// <summary>Gets or sets the item's label: its value while the list is selectable.</summary>
    [Parameter]
    public string Label { get; set; } = string.Empty;

    /// <summary>Gets or sets the item's role.</summary>
    [Parameter]
    public string Role { get; set; } = "menuitem";

    [Parameter]
    public bool Disabled { get; set; }

    protected override string DefaultTagName => "div";

    // The pipeline reads the state before the part's attributes, so the record follows the flags and the mirror here.
    protected override FixtureListItemState State
    {
        get
        {
            bool selected = Selected;
            bool highlighted = item!.Highlighted;
            if (state is not { } current || current.Disabled != Disabled || current.Selected != selected || current.Highlighted != highlighted)
            {
                state = new FixtureListItemState(Disabled, selected, highlighted);
            }

            return state;
        }
    }

    protected override bool UsesGeneratedId => true;

    private bool Selected => HostContext is { Selectable: true } host && string.Equals(host.Value, Label, StringComparison.Ordinal);

    protected override void OnInitialized()
    {
        button = new global::Blazix.Primitives.ButtonBehavior(JSRuntime, Logger, DevelopmentLog, IdGenerator, StateHasChanged, DispatchExceptionAsync, onKeyDown, onKeyUp);
        item = new CompositeItemBehavior(RootContext, nameof(FixtureListItem), onKeyDown, StateHasChanged);
        base.OnInitialized();
    }

    protected override void OnParametersSet()
    {
        button!.Disabled = Disabled;
        button.Native = false;
        button.FocusableWhenDisabled = true;
        button.SetConsumerAttributes(AdditionalAttributes);
        item!.SetRegistration(Id, AdditionalAttributes, Disabled, focusableWhenDisabled: true);
        item.SetConsumerAttributes(Disabled ? null : AdditionalAttributes);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await button!.AfterRenderAsync(Enabled, Element, Id, composite: true);
        await item!.AfterRenderAsync(Element);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            button?.Dispose();
            item?.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void AddPartAttributes(AttributeList attributes)
    {
        button!.AddAttributes(attributes, Id, composite: true);
        attributes.Set("role", Role);
        if (HostContext is { Selectable: true })
        {
            attributes.Set("aria-selected", Selected ? "true" : "false");
        }

        attributes.Set("onclick", onClick);
        item!.AddAttributes(attributes);
    }

    protected override bool ForwardsConsumerHandler(string eventName) =>
        button!.ForwardsConsumerHandler(eventName) && item!.ForwardsConsumerHandler(eventName);

    private static bool IsItemKey(string? key) =>
        key is not null && (key.StartsWith(ArrowKeyPrefix, StringComparison.Ordinal) || key is "Home" or "End");

    private Task OnClickAsync(MouseEventArgs e) =>
        Disabled || HostContext is null ? Task.CompletedTask : HostContext.ActivateAsync(Label, EventSnapshot.FromEventArgs(e));
}
