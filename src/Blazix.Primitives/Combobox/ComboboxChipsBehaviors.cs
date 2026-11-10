using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace Blazix.Primitives;

// combobox/chips/ComboboxChips.tsx (CMB §3.4, STR-09, STA-33): role="toolbar" while a value exists, a second registry
// (CompositeListBehavior in registration-only mode: the composite module reports the chips' document order); its `combobox`
// handle owns the chip keys and focus moves (KEY-07..10), with the chips' ids in that order, and the input press (PTR-08).
// Its id, which Base UI does not render, is the input's and the dismissal's way to the container. Combobox only.
/// <summary>The behavior of a combobox chips container.</summary>
internal sealed class ComboboxChipsBehavior : IAsyncDisposable
{
    private readonly ComboboxPartServices services;

    private readonly CompositeListBehavior list;

    private readonly ComboboxChipsContext context;

    private ComboboxStore? store;

    private ComboboxTwin? twin;

    private TwinHandle<ComboboxOptions>? handle;

    private ComboboxOptions? sentOptions;

    private string[] chipIds = [];

    private string? id;

    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="ComboboxChipsBehavior"/> class.</summary>
    /// <param name="services">The part's services.</param>
    public ComboboxChipsBehavior(ComboboxPartServices services)
    {
        this.services = services;
        list = new CompositeListBehavior(services.JSRuntime, services.Ids, services.RequestRender);
        context = new ComboboxChipsContext(list.Context, OnChipsChanged);
    }

    private ComboboxStore Store => store ?? throw new InvalidOperationException($"Blazix: `{services.Family}Chips` is used before its first render.");

    /// <summary>Gets the context the chips register through.</summary>
    public ComboboxChipsContext Context => context;

    /// <summary>Gets or sets a value indicating whether the reading direction is right to left, for the chip keys.</summary>
    public bool Rtl { get; set; }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (store is not null && id is not null)
        {
            string own = id;
            store.SetElementIds(ids => ids.Chips == own ? ids with { Chips = null } : ids);
        }

        list.Dispose();
        if (twin is not null)
        {
            await twin.DisposeAsync();
        }
    }

    /// <summary>Reads the root and subscribes the part.</summary>
    /// <param name="cascaded">The cascaded store, or <see langword="null"/> outside a root.</param>
    /// <param name="subscribe">The part's subscription factory over the store.</param>
    public void Subscribe(ComboboxStore? cascaded, Func<ComboboxStore, SelectorSubscription<ComboboxStoreState>> subscribe)
    {
        store = ComboboxParts.RequireRoot(cascaded, services.Family);
        SelectorSubscription<ComboboxStoreState> subscription = subscribe(store);
        subscription.Select(static state => state.HasSelectedValue);
        subscription.Select(static state => state.Disabled);
        subscription.Select(static state => state.ReadOnly);
        subscription.Select(static state => state.ElementIds.Input);
        list.ItemsChanged += services.RequestRender;
        twin = new ComboboxTwin(services.JSRuntime);
        twin.InputPressed += snapshot => _ = Observe(services.InvokeAsync(() => OnInputPressAsync(snapshot)));
        twin.RemoveRequested += request => _ = Observe(services.InvokeAsync(() => RemoveAsync(request)));
        twin.OpenRequested += snapshot => _ = Observe(services.InvokeAsync(() => Store.RequestOpenAsync(true, new OpenChangeEventDetails(EventReason.ListNavigation, snapshot))));
    }

    /// <summary>Applies the id the part renders and publishes it.</summary>
    /// <param name="id">The consumer's id or a generated one.</param>
    public void Apply(string? id)
    {
        this.id = id;
        string? own = id;
        Store.SetElementIds(ids => ids with { Chips = own });
    }

    /// <summary>Adds the container's attributes.</summary>
    /// <param name="attributes">The part's attributes.</param>
    public void AddPartAttributes(AttributeList attributes)
    {
        // ComboboxChips.tsx:30-31: NVDA stays in focus mode inside a toolbar.
        if (Store.State.HasSelectedValue)
        {
            attributes.Set("role", "toolbar");
        }

        list.AddAttributes(attributes, id);
        attributes.Set("id", id);
    }

    /// <summary>Renders the chips context around the container's content.</summary>
    /// <param name="builder">The part's render tree builder.</param>
    /// <param name="content">The container's content.</param>
    public void BuildContent(RenderTreeBuilder builder, RenderFragment? content)
    {
        builder.OpenComponent<CascadingValue<ComboboxChipsContext>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<ComboboxChipsContext>.Value), context);
        builder.AddComponentParameter(2, nameof(CascadingValue<ComboboxChipsContext>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<ComboboxChipsContext>.ChildContent), content);
        builder.CloseComponent();
    }

    /// <summary>Attaches the registry and the container's handle and sends their changed options.</summary>
    /// <param name="element">The container's element.</param>
    /// <returns>A task that completes when the handles have their options.</returns>
    public async Task AfterRenderAsync(ElementReference? element)
    {
        if (disposed)
        {
            return;
        }

        Task listed = list.AfterRenderAsync(true, element, id);
        await Task.WhenAll(listed, SyncTwinAsync(element));
    }

    private void OnChipsChanged()
    {
        string[] next = context.OrderedIds();
        if (!next.AsSpan().SequenceEqual(chipIds))
        {
            chipIds = next;
            services.RequestRender();
        }
    }

    private async Task SyncTwinAsync(ElementReference? element)
    {
        if (twin is null || element is not { } target)
        {
            return;
        }

        ComboboxStoreState state = Store.State;
        ComboboxOptions options = new()
        {
            Part = ComboboxTwin.ChipsPart,
            Disabled = state.Disabled,
            ReadOnly = state.ReadOnly,
            Rtl = Rtl,
            InputId = state.ElementIds.Input,
            ChipIds = chipIds,
        };
        if (handle is null)
        {
            sentOptions = options;
            handle = await twin.AttachAsync(target, options);
            return;
        }

        if (options != sentOptions)
        {
            sentOptions = options;
            await handle.UpdateAsync(options);
        }
    }

    // combobox/utils/handleInputPress.ts:35-37.
    private Task OnInputPressAsync(EventSnapshot snapshot)
    {
        ComboboxStore root = Store;
        return root.State.Disabled || !root.OpenOnInputClick
            ? Task.CompletedTask
            : root.RequestOpenAsync(true, new OpenChangeEventDetails(EventReason.InputPress, snapshot));
    }

    // ComboboxChip.tsx:62-80: Backspace or Delete on a chip clears the highlight, then removes its value with the reason none.
    private async Task RemoveAsync(ComboboxRequest request)
    {
        ComboboxStore root = Store;
        await root.ClearHighlightAsync(request.Event);
        await root.RemoveAtAsync(request.Index, EventReason.None, request.Event);
    }

    private async Task Observe(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception exception) when (!task.IsCanceled)
        {
            await services.DispatchException(exception);
        }
    }
}

// combobox/chip/ComboboxChip.tsx (CMB §3.5): tabindex="-1", aria-disabled, aria-readonly, registered in the chips' list by
// its id (a generated one, which Base UI does not render); a Chip outside Chips throws a missing-context error (§16 Q7).
// Its keys are the chips container's handle's. Combobox only.
/// <summary>The behavior of a combobox chip.</summary>
internal sealed class ComboboxChipBehavior : IDisposable
{
    private readonly ComboboxPartServices services;

    private readonly ComboboxChipContext context = new();

    private ComboboxStore? store;

    private ComboboxChipsContext? chips;

    private CompositeListItemBehavior? item;

    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="ComboboxChipBehavior"/> class.</summary>
    /// <param name="services">The part's services.</param>
    public ComboboxChipBehavior(ComboboxPartServices services)
    {
        this.services = services;
    }

    private ComboboxStore Store => store ?? throw new InvalidOperationException($"Blazix: `{services.Family}Chip` is used before its first render.");

    /// <summary>Gets the chip context its remove button reads.</summary>
    public ComboboxChipContext Context => context;

    /// <summary>Gets a value indicating whether the chip is disabled.</summary>
    public bool Disabled => Store.State.Disabled;

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        item?.Dispose();
        chips?.Remove(context);
    }

    /// <summary>Reads the root and the chips and subscribes the part.</summary>
    /// <param name="cascaded">The cascaded store, or <see langword="null"/> outside a root.</param>
    /// <param name="chipsContext">The cascaded chips context, or <see langword="null"/>.</param>
    /// <param name="subscribe">The part's subscription factory over the store.</param>
    public void Subscribe(ComboboxStore? cascaded, ComboboxChipsContext? chipsContext, Func<ComboboxStore, SelectorSubscription<ComboboxStoreState>> subscribe)
    {
        store = ComboboxParts.RequireRoot(cascaded, services.Family);
        chips = chipsContext ?? throw ComboboxParts.MissingChips(services.Family);
        item = new CompositeListItemBehavior(chips.List, OnIndexChanged);
        SelectorSubscription<ComboboxStoreState> subscription = subscribe(store);
        subscription.Select(static state => state.Disabled);
        subscription.Select(static state => state.ReadOnly);
        chips.Add(context);
    }

    /// <summary>Applies the id the part renders and registers it.</summary>
    /// <param name="id">The consumer's id or a generated one.</param>
    public void Apply(string? id)
    {
        item!.SetRegistration(id);
        if (!string.Equals(context.Id, item.Id, StringComparison.Ordinal))
        {
            context.Id = item.Id;
            chips!.Changed();
        }
    }

    /// <summary>Adds the chip's attributes.</summary>
    /// <param name="attributes">The part's attributes.</param>
    public void AddPartAttributes(AttributeList attributes)
    {
        ComboboxStoreState state = Store.State;
        attributes.Set("tabindex", "-1");
        if (state.Disabled)
        {
            attributes.Set("aria-disabled", "true");
        }

        if (state.ReadOnly)
        {
            attributes.Set("aria-readonly", "true");
        }

        attributes.Set("id", context.Id);
    }

    /// <summary>Renders the chip context around the chip's content.</summary>
    /// <param name="builder">The part's render tree builder.</param>
    /// <param name="content">The chip's content.</param>
    public void BuildContent(RenderTreeBuilder builder, RenderFragment? content)
    {
        builder.OpenComponent<CascadingValue<ComboboxChipContext>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<ComboboxChipContext>.Value), context);
        builder.AddComponentParameter(2, nameof(CascadingValue<ComboboxChipContext>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<ComboboxChipContext>.ChildContent), content);
        builder.CloseComponent();
    }

    /// <summary>Sends the chip's element to the registry.</summary>
    /// <param name="element">The chip's element.</param>
    /// <returns>A task that completes when the registry has it.</returns>
    public Task AfterRenderAsync(ElementReference? element) => disposed || item is null ? Task.CompletedTask : item.AfterRenderAsync(element);

    private void OnIndexChanged()
    {
        context.SetIndex(item!.Index);
        chips!.Changed();
    }
}

// combobox/chip-remove/ComboboxChipRemove.tsx (CMB §3.6): a native button, tabindex="-1", focusable when disabled (disabled
// while read-only too); its activation is the `combobox` twin's `remove` point (fallback stop; ADR 04 Amendments 2026-11-03
// (3)): on WebAssembly the removal runs inside the dispatch and its details decide the propagation, on Server it runs from
// the report. The removal's reason is chip-remove-press. Combobox only.
/// <summary>The behavior of a combobox chip's remove button.</summary>
internal sealed class ComboboxChipRemoveBehavior : IAsyncDisposable
{
    private readonly ComboboxPartServices services;

    private readonly ButtonBehavior button;

    private ComboboxStore? store;

    private ComboboxChipContext? chip;

    private ComboboxTwin? twin;

    private TwinHandle<ComboboxOptions>? handle;

    private ComboboxOptions? sentOptions;

    private bool disabledParameter;

    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="ComboboxChipRemoveBehavior"/> class.</summary>
    /// <param name="services">The part's services.</param>
    /// <param name="developmentLog">The button mechanism's development log.</param>
    /// <param name="onKeyDown">The part's own <c>onkeydown</c> handler.</param>
    /// <param name="onKeyUp">The part's own <c>onkeyup</c> handler.</param>
    public ComboboxChipRemoveBehavior(ComboboxPartServices services, ButtonDevelopmentLog developmentLog, EventCallback onKeyDown, EventCallback onKeyUp)
    {
        this.services = services;
        button = new ButtonBehavior(services.JSRuntime, services.Logger, developmentLog, services.Ids, services.RequestRender, services.DispatchException, onKeyDown, onKeyUp);
    }

    private ComboboxStore Store => store ?? throw new InvalidOperationException($"Blazix: `{services.Family}ChipRemove` is used before its first render.");

    /// <summary>Gets a value indicating whether the button is disabled, by the part or the root.</summary>
    public bool Disabled => disabledParameter || Store.State.Disabled;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (chip is not null)
        {
            chip.IndexChanged -= services.RequestRender;
        }

        button.Dispose();
        if (twin is not null)
        {
            twin.Decider = null;
            await twin.DisposeAsync();
        }
    }

    /// <summary>Reads the root and the chip and subscribes the part.</summary>
    /// <param name="cascaded">The cascaded store, or <see langword="null"/> outside a root.</param>
    /// <param name="chipContext">The cascaded chip context, or <see langword="null"/>.</param>
    /// <param name="subscribe">The part's subscription factory over the store.</param>
    public void Subscribe(ComboboxStore? cascaded, ComboboxChipContext? chipContext, Func<ComboboxStore, SelectorSubscription<ComboboxStoreState>> subscribe)
    {
        store = ComboboxParts.RequireRoot(cascaded, services.Family);
        chip = chipContext ?? throw ComboboxParts.MissingChip(services.Family);
        chip.IndexChanged += services.RequestRender;
        SelectorSubscription<ComboboxStoreState> subscription = subscribe(store);
        subscription.Select(static state => state.Disabled);
        subscription.Select(static state => state.ReadOnly);
        subscription.Select(static state => state.ElementIds.Input);
        twin = new ComboboxTwin(services.JSRuntime) { Decider = Decide };
        twin.RemoveRequested += request => _ = Observe(services.InvokeAsync(() => Store.RemoveAtAsync(request.Index, EventReason.ChipRemovePress, request.Event)));
    }

    /// <summary>Applies the part's parameters.</summary>
    /// <param name="nativeButton">The part's <c>NativeButton</c>.</param>
    /// <param name="disabled">The part's <c>Disabled</c>.</param>
    /// <param name="consumerAttributes">The consumer's attributes.</param>
    public void Apply(bool nativeButton, bool disabled, IReadOnlyDictionary<string, object>? consumerAttributes)
    {
        disabledParameter = disabled;
        button.Native = nativeButton;
        button.FocusableWhenDisabled = true;
        button.Disabled = Disabled || Store.State.ReadOnly;
        button.SetConsumerAttributes(consumerAttributes);
    }

    /// <summary>Adds the button's attributes.</summary>
    /// <param name="attributes">The part's attributes.</param>
    /// <param name="id">The id the part renders, or <see langword="null"/>.</param>
    public void AddPartAttributes(AttributeList attributes, string? id)
    {
        button.Disabled = Disabled || Store.State.ReadOnly;
        button.AddAttributes(attributes, id, composite: false);
        attributes.Set("tabindex", "-1");
    }

    /// <summary>Determines whether the consumer's handler for an event renders on the element.</summary>
    /// <param name="eventName">The event's attribute name.</param>
    /// <returns><see langword="false"/> for a handler the part declines and runs itself.</returns>
    public bool ForwardsConsumerHandler(string eventName) => button.ForwardsConsumerHandler(eventName);

    /// <summary>Runs the declined consumer <c>onkeydown</c>.</summary>
    /// <param name="args">The keydown.</param>
    /// <returns>A task that completes when the handler has run.</returns>
    public Task HandleKeyDownAsync(KeyboardEventArgs args) => button.HandleKeyDownAsync(args);

    /// <summary>Runs the declined consumer <c>onkeyup</c>.</summary>
    /// <param name="args">The keyup.</param>
    /// <returns>A task that completes when the handler has run.</returns>
    public Task HandleKeyUpAsync(KeyboardEventArgs args) => button.HandleKeyUpAsync(args);

    /// <summary>Attaches the button and the remove handle and sends their changed options.</summary>
    /// <param name="element">The button's element.</param>
    /// <param name="id">The id the part renders, or <see langword="null"/>.</param>
    /// <returns>A task that completes when the handles have their options.</returns>
    public async Task AfterRenderAsync(ElementReference? element, string? id)
    {
        if (disposed)
        {
            return;
        }

        button.Disabled = Disabled || Store.State.ReadOnly;
        await Task.WhenAll(button.AfterRenderAsync(true, element, id, composite: false), SyncTwinAsync(element));
    }

    private async Task SyncTwinAsync(ElementReference? element)
    {
        if (twin is null || element is not { } target)
        {
            return;
        }

        ComboboxStoreState state = Store.State;
        ComboboxOptions options = new()
        {
            Part = ComboboxTwin.ChipRemovePart,
            Disabled = Disabled,
            ReadOnly = state.ReadOnly,
            InputId = state.ElementIds.Input,
            Index = chip!.Index,
        };
        if (handle is null)
        {
            sentOptions = options;
            handle = await twin.AttachAsync(target, options);
            return;
        }

        if (options != sentOptions)
        {
            sentOptions = options;
            await handle.UpdateAsync(options);
        }
    }

    // CMB-PTR-10, KEY-10 (ComboboxChipRemove.tsx:100-114): on WebAssembly the removal runs inside the dispatch; the event stops unless
    // the change callback allowed its propagation.
    private DecisionPolicy Decide(string point, EventSnapshot snapshot)
    {
        if (!string.Equals(point, ComboboxTwin.RemovePoint, StringComparison.Ordinal))
        {
            return DecisionPolicy.Run;
        }

        Task<ChangeEventDetails?> removing = Store.RemoveAtAsync(chip!.Index, EventReason.ChipRemovePress, snapshot);
        if (!removing.IsCompleted)
        {
            _ = Observe(removing);
            return DecisionPolicy.Stop;
        }

        return removing.Result is { IsPropagationAllowed: true } ? DecisionPolicy.Run : DecisionPolicy.Stop;
    }

    private async Task Observe(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception exception) when (!task.IsCanceled)
        {
            await services.DispatchException(exception);
        }
    }
}
