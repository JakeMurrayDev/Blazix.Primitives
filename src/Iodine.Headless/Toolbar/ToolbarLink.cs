using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Blazix.Primitives;

// TLB-STA-01; toolbar/link/ToolbarLink.tsx:28-30,46-51. ADR 08 §3: the state record is sealed, partial and [PartState]; the
// default projection gives data-orientation (TLB-DOM-06).
/// <summary>Represents the state of a toolbar link, which the part exposes as the <c>data-orientation</c> attribute.</summary>
/// <param name="Orientation">The toolbar's orientation.</param>
[PartState]
public sealed partial record ToolbarLinkState(Orientation Orientation);

// TLB §3.3; toolbar/link/ToolbarLinkDataAttributes.ts:5.
/// <summary>Provides the names of the data attributes a toolbar link renders.</summary>
public static class ToolbarLinkDataAttributes
{
    /// <summary>Represents the attribute that holds the toolbar's orientation, <c>horizontal</c> or <c>vertical</c>: <c>data-orientation</c>.</summary>
    public const string Orientation = "data-orientation";
}

// Base UI's Toolbar.Link (toolbar/link/ToolbarLink.tsx); the contract is docs/components/toolbar.md.
// - One `a` that is a composite item of the toolbar and nothing else: no button mechanism (TLB-KEY-16, §16 Q6), no
//   parameters of its own (TLB-API-11), never a disabled attribute whatever the toolbar's or a group's state (TLB-DOM-06,
//   TLB-ARIA-05, TLB-EDGE-03). The item's tabindex="-1" is rendered (CMP-STA-15) and the `composite` module owns it from
//   attach (ADR 04 Amendments 2026-10-22); the consumer's attributes win (TLB-DOM-11).
// - The link draws the id it registers (ADR 04 Amendments 2026-10-22, the RadioRoot precedent).
// - The item gate (ADR 04 Amendments 2026-10-26, chosen by the user 2026-10-26): the item declines the consumer's onkeydown
//   and the part's handler runs it unless the root's `navigate` decision already did; Enter is the browser's activation.
/// <summary>
/// Represents a link in a toolbar: Base UI's <c>Toolbar.Link</c>, rendered as an <c>a</c> element that the toolbar's arrow
/// keys move focus to.
/// </summary>
/// <remarks>
/// A link is never disabled: neither the toolbar's <c>Disabled</c> nor a group's reaches it. Its <c>href</c> and every
/// other attribute are forwarded unchanged.
/// </remarks>
public sealed class ToolbarLink : PartBase<ToolbarLinkState>
{
    private const string DefaultTag = "a";

    private readonly EventCallback onKeyDown;
    private readonly Action toolbarChanged;
    private ToolbarRootContext? toolbar;
    private CompositeItemBehavior? item;
    private ToolbarLinkState? state;

    /// <summary>Initializes a new instance of the <see cref="ToolbarLink"/> class.</summary>
    public ToolbarLink()
    {
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", e => item!.HandleKeyDownAsync(e));
        toolbarChanged = OnToolbarChanged;
    }

    [CascadingParameter]
    private ToolbarRootContext? ToolbarContext { get; set; }

    [CascadingParameter]
    private CompositeRootContext? CompositeContext { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override ToolbarLinkState State => state!;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        // TLB-EDGE-01: the toolbar's error comes before the composite item's.
        toolbar = ToolbarRootContext.Require(ToolbarContext);
        item = new CompositeItemBehavior(CompositeContext, "ToolbarLink", onKeyDown);
        base.OnInitialized();
        toolbar.AddListener(toolbarChanged);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // TLB-STA-03 (toolbar/link/ToolbarLink.tsx:8-12): a link is never disabled and holds a focusable item slot.
        item!.SetRegistration(Id, AdditionalAttributes, disabled: false, focusableWhenDisabled: true);
        item.SetConsumerAttributes(AdditionalAttributes);
        UpdateState();
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => item!.AfterRenderAsync(Element);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            item?.Dispose();
            toolbar?.RemoveListener(toolbarChanged);
        }

        base.Dispose(disposing);
    }

    // CMP-STA-15: the item's tabindex="-1", and its key handler when the consumer supplied one; the consumer's tabindex wins.
    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes) => item!.AddAttributes(attributes);

    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName) => item!.ForwardsConsumerHandler(eventName);

    // A change of the toolbar's state: render only when the orientation changed (the disabled state never reaches a link).
    private void OnToolbarChanged()
    {
        if (UpdateState())
        {
            StateHasChanged();
        }
    }

    // Returns whether the state changed; a new record is made only then (ADR 16 rule 7).
    private bool UpdateState()
    {
        Orientation orientation = toolbar!.Orientation;
        if (state is { } current && current.Orientation == orientation)
        {
            return false;
        }

        state = new ToolbarLinkState(orientation);
        return true;
    }
}
