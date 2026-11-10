using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// combobox/input-group/ComboboxInputGroup.tsx (CMB §3.3, §5.3): role="group", the pressable trigger mapping plus
// data-popup-side, data-list-empty, data-placeholder and the field validity (combobox/utils/stateAttributesMapping.ts). Its
// `combobox` handle runs handleInputPress (CMB-PTR-08): a press away from the input's interactive neighbours focuses the
// input and reports the press, which opens the popup with `input-press` when the root opens on input click. Its id, which
// Base UI does not render, joins the dismissal's exemption list (CMB-DIS-01) and anchors the positioner (CMB-API-50).
/// <summary>The behavior of a combobox-family input group: its state, its id and its input press.</summary>
internal sealed class ComboboxInputGroupBehavior : IAsyncDisposable
{
    private readonly ComboboxPartServices services;

    private ComboboxStore? store;

    private FieldRootContext? fieldScope;

    private ComboboxTwin? twin;

    private TwinHandle<ComboboxOptions>? handle;

    private ComboboxOptions? sentOptions;

    private string? id;

    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="ComboboxInputGroupBehavior"/> class.</summary>
    /// <param name="services">The part's services.</param>
    public ComboboxInputGroupBehavior(ComboboxPartServices services)
    {
        this.services = services;
    }

    /// <summary>Gets the root's store.</summary>
    public ComboboxStore Store => store ?? throw new InvalidOperationException($"Blazix: `{services.Family}InputGroup` is used before its first render.");

    /// <summary>Gets the field's state the group projects, with the root's disabled state.</summary>
    public FieldStateSnapshot Field => (fieldScope?.State ?? FieldStateSnapshot.Default) with { Disabled = Store.State.Disabled };

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        fieldScope?.RemoveListener(services.RequestRender);
        if (store is not null && id is not null)
        {
            string own = id;
            store.SetElementIds(ids => ids.InputGroup == own ? ids with { InputGroup = null } : ids);
        }

        if (twin is not null)
        {
            await twin.DisposeAsync();
        }
    }

    /// <summary>Reads the root and the field and subscribes the part.</summary>
    /// <param name="cascaded">The cascaded store, or <see langword="null"/> outside a root.</param>
    /// <param name="field">The cascaded field, or <see langword="null"/>.</param>
    /// <param name="subscribe">The part's subscription factory over the store.</param>
    public void Subscribe(ComboboxStore? cascaded, FieldRootContext? field, Func<ComboboxStore, SelectorSubscription<ComboboxStoreState>> subscribe)
    {
        store = ComboboxParts.RequireRoot(cascaded, services.Family);
        fieldScope = field;
        fieldScope?.AddListener(services.RequestRender);
        SelectorSubscription<ComboboxStoreState> subscription = subscribe(store);
        subscription.Select(ComboboxStoreState.Open);
        subscription.Select(ComboboxStoreState.ListEmpty);
        subscription.Select(static state => state.Lifecycle.Mounted ? state.PopupSide : null);
        subscription.Select(static state => state.Disabled);
        subscription.Select(static state => state.ReadOnly);
        subscription.Select(static state => state.HasSelectedValue);
        subscription.Select(static state => state.ElementIds.Input);
        subscription.Select(static state => state.ElementIds.Chips);
        twin = new ComboboxTwin(services.JSRuntime);
        twin.InputPressed += snapshot => _ = Observe(services.InvokeAsync(() => OnInputPressAsync(snapshot)));
    }

    /// <summary>Applies the part's rendered id and publishes it to the store.</summary>
    /// <param name="id">The id the part renders: the consumer's or a generated one.</param>
    public void Apply(string? id)
    {
        this.id = id;
        string? own = id;
        Store.SetElementIds(ids => ids with { InputGroup = own });
    }

    /// <summary>Adds the group's attributes.</summary>
    /// <param name="attributes">The part's attributes.</param>
    public void AddPartAttributes(AttributeList attributes)
    {
        attributes.Set("role", "group");
        attributes.Set("id", id);
    }

    /// <summary>Attaches the group's handle and sends its changed options.</summary>
    /// <param name="element">The group's element.</param>
    /// <returns>A task that completes when the handle has its options.</returns>
    public async Task AfterRenderAsync(ElementReference? element)
    {
        if (disposed || twin is null || element is not { } target)
        {
            return;
        }

        ComboboxStoreState state = Store.State;
        ComboboxOptions options = new()
        {
            Part = ComboboxTwin.InputGroupPart,
            Disabled = state.Disabled,
            InputId = state.ElementIds.Input,
            ChipsId = state.ElementIds.Chips,
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

    // combobox/utils/handleInputPress.ts:35-37: the module focused the input; the press opens when the root opens on input click.
    private Task OnInputPressAsync(EventSnapshot snapshot)
    {
        ComboboxStore root = Store;
        return root.State.Disabled || !root.OpenOnInputClick
            ? Task.CompletedTask
            : root.RequestOpenAsync(true, new OpenChangeEventDetails(EventReason.InputPress, snapshot));
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
