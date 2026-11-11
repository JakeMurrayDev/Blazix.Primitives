using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// SEL-STA-17 (select/icon/SelectIcon.tsx:23-25, 37-42).
/// <summary>Represents the state of a select icon, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Open">A value indicating whether the popup is open; projected as <c>data-popup-open</c>.</param>
[PartState]
public sealed partial record SelectIconState(bool Open);

// select/icon/SelectIconDataAttributes.ts (SEL-DOM-13).
/// <summary>Provides the names of the data attributes a select icon renders.</summary>
public static class SelectIconDataAttributes
{
    /// <summary>Represents the attribute present while the popup is open: <c>data-popup-open</c>.</summary>
    public const string PopupOpen = PopupTriggerDataAttributes.PopupOpen;
}

// Base UI's Select.Icon (select/icon/SelectIcon.tsx); SEL-API-31, SEL-DOM-13, SEL-ARIA-13: a `span` with `aria-hidden`
// (a consumer's wins) and the default glyph ▼, the trigger mapping's `data-popup-open` while open.
/// <summary>Represents an icon that marks a select's trigger, Base UI's <c>Select.Icon</c>, rendered as a <c>span</c>.</summary>
/// <remarks>Without content it shows ▼.</remarks>
public sealed class SelectIcon : PartBase<SelectIconState>
{
    private const string DefaultGlyph = "▼";

    private static readonly StateAttributesMapping<SelectIconState> s_mapping = new StateAttributesMapping<SelectIconState>().Map("Open", PopupStateMapping.TriggerOpen);
    private static readonly SelectIconState s_open = new(true);
    private static readonly SelectIconState s_closed = new(false);
    private static readonly RenderFragment s_glyph = static builder => builder.AddContent(0, DefaultGlyph);

    private StoreSelection<SelectStoreState, bool>? open;

    [CascadingParameter]
    private SelectStore? RootStore { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "span";

    /// <inheritdoc />
    protected override SelectIconState State => open!.Value ? s_open : s_closed;

    /// <inheritdoc />
    protected override StateAttributesMapping<SelectIconState>? StateAttributesMapping => s_mapping;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The icon is not inside a <see cref="SelectRoot{TValue}"/>.</exception>
    protected override void OnInitialized()
    {
        SelectStore.Require(RootStore);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet() => ChildContent ??= s_glyph;

    /// <inheritdoc />
    protected override void SubscribeSelectors() => open = SubscribeTo<SelectStoreState>(RootStore!).Select(SelectStoreState.Open);

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes) => attributes.Set("aria-hidden", "true");
}
