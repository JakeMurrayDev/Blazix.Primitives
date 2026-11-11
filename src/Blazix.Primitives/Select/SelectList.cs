using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Blazix.Primitives.Utilities;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// Base UI's Select.List (select/list/SelectList.tsx); SEL-STR-08, SEL-DOM-31..33, SEL-ARIA-08: the listbox and the scroll
// container while mounted (the Popup becomes presentational), `<root id>-list`, `aria-multiselectable`, `aria-readonly`,
// no `aria-orientation`; the scrollbar class while a scroll arrow is mounted and the popup was not opened by touch; its
// aligned layout style is the `select` module's (SEL-DOM-32, JS).
/// <summary>Represents the list of a select's items, Base UI's <c>Select.List</c>, rendered as a <c>div</c>.</summary>
public sealed class SelectList : PartBase<EmptyState>
{
    private SelectStore? store;
    private SelectModuleAttachment? module;
    private string? consumerStyle;
    private string? frozenStyle;
    private StoreSelection<SelectStoreState, string?>? rootId;
    private StoreSelection<SelectStoreState, bool>? multiple;
    private StoreSelection<SelectStoreState, bool>? readOnly;
    private StoreSelection<SelectStoreState, bool>? hasScrollArrows;
    private StoreSelection<SelectStoreState, bool>? touchOpened;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private SelectStore? RootStore { get; set; }

    [CascadingParameter]
    private SelectPositionerContext? PositionerContext { get; set; }

    [CascadingParameter]
    private PopupPortalContext? Portal { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override EmptyState State => EmptyState.Instance;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The list is not inside a <see cref="SelectRoot{TValue}"/> and a <see cref="SelectPositioner"/>.</exception>
    protected override void OnInitialized()
    {
        store = SelectStore.Require(RootStore);
        SelectPositionerContext.Require(PositionerContext);
        store.SetHasList(true);
        module = new SelectModuleAttachment(JSRuntime);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        store!.SetListboxConsumerId(list: true, ConsumerId());
        base.OnParametersSet();
    }

    // SEL-DOM-32: the list's style is the `select` module's from attach (C# → JS): the consumer's under the aligned layout.
    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (Element is not { } element)
        {
            return;
        }

        await PopupPortalContext.WhenMoved(Portal);
        SelectPositionerContext positionerContext = PositionerContext!;
        if (!string.Equals(positionerContext.ListElement?.Id, element.Id, StringComparison.Ordinal))
        {
            positionerContext.ListElement = element;
            _ = positionerContext.SyncAsync();
        }

        await module!.SyncAsync(element, new SelectOptions { Part = SelectTwin.ListPart, Style = consumerStyle ?? string.Empty });
        frozenStyle ??= consumerStyle ?? string.Empty;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && store is not null)
        {
            store.SetHasList(false);
            store.SetListboxConsumerId(list: true, null);
            PositionerContext!.ListElement = null;
            if (module is not null)
            {
                _ = module.DisposeAsync().AsTask();
            }
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void SubscribeSelectors()
    {
        SelectorSubscription<SelectStoreState> subscription = SubscribeTo<SelectStoreState>(store!);
        rootId = subscription.Select(SelectStoreState.RootIdSelector);
        multiple = subscription.Select(SelectStoreState.MultipleSelector);
        readOnly = subscription.Select(SelectStoreState.ReadOnlySelector);
        hasScrollArrows = subscription.Select(SelectStoreState.HasScrollArrows);
        touchOpened = subscription.Select(SelectStoreState.TouchOpened);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        if (rootId!.Value is { } id)
        {
            attributes.Set("id", id + "-list");
        }

        attributes.Set("role", "listbox");
        if (multiple!.Value)
        {
            attributes.Set("aria-multiselectable", "true");
        }

        if (readOnly!.Value)
        {
            attributes.Set("aria-readonly", "true");
        }

        if (hasScrollArrows!.Value && !touchOpened!.Value)
        {
            attributes.Set(MergeProps.ClassAttribute, DisableScrollbarStyle.ClassName);
        }
    }

    // The handover at attach (ADR 04 Amendments 2026-10-18): C# renders the consumer's style until the handle attaches,
    // then the style it rendered, and sends the consumer's through the module.
    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes)
    {
        consumerStyle = attributes.TryGetValue(MergeProps.StyleAttribute, out AttributeValue merged) && merged.TryGetValue(out string text) && text.Length > 0 ? text : null;
        if (frozenStyle is null)
        {
            return;
        }

        if (frozenStyle.Length == 0)
        {
            attributes.Remove(MergeProps.StyleAttribute);
        }
        else
        {
            attributes.Set(MergeProps.StyleAttribute, frozenStyle);
        }
    }

    private string? ConsumerId() => AdditionalAttributes is not null && AdditionalAttributes.TryGetValue("id", out object? supplied) && supplied is string text ? text : null;
}
