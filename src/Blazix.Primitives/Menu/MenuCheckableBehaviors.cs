using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// MNU-API-41/42, MNU-EVT-08 (menu/checkbox-item/MenuCheckboxItem.tsx:26-29, 112, 165-200): the checked state is held by the
// part, above its element (Controlled<bool>, fixed at the first parameter set), toggled by the item's activation; the
// change is cancelable (reason `item-press`). ContextMenuCheckboxItem (track 7) is a shell over it.
/// <summary>The behavior of a menu checkbox item's checked state.</summary>
internal sealed class MenuCheckboxItemBehavior
{
    private readonly string familyName;
    private readonly Action requestRender;
    private readonly MenuCheckableItemContext itemContext = new();
    private Controlled<bool>? state;
    private EventCallback<bool> checkedChanged;
    private EventCallback<ValueChangeEventArgs<bool>> onCheckedChange;

    /// <summary>Initializes a new instance of the <see cref="MenuCheckboxItemBehavior"/> class.</summary>
    /// <param name="familyName">The item's family, such as <c>Menu</c>.</param>
    /// <param name="requestRender">Asks the part to render.</param>
    public MenuCheckboxItemBehavior(string familyName, Action requestRender)
    {
        this.familyName = familyName;
        this.requestRender = requestRender;
    }

    /// <summary>Gets a value indicating whether the item is checked.</summary>
    public bool Checked => state?.Value ?? false;

    /// <summary>Gets the context the item cascades to its indicator.</summary>
    public MenuCheckableItemContext ItemContext => itemContext;

    /// <summary>Reads whether <c>Checked</c> was supplied, fixing control at the first call; call it from the part's <c>SetParametersAsync</c>.</summary>
    /// <param name="parameters">The part's parameters.</param>
    /// <param name="logger">The part's logger, which receives the controlled-state warnings.</param>
    public void SetParameters(ParameterView parameters, ILogger? logger)
    {
        state ??= new Controlled<bool>(familyName + "CheckboxItem", "checked", logger);
        state.SetParameters(parameters, "Checked", "DefaultChecked");
    }

    /// <summary>Applies the callbacks and the item's state its indicator reads.</summary>
    /// <param name="checkedChanged">The <c>CheckedChanged</c> callback.</param>
    /// <param name="onCheckedChange">The <c>OnCheckedChange</c> callback.</param>
    /// <param name="disabled">The item's <c>Disabled</c>.</param>
    /// <param name="highlighted">Whether the item is highlighted.</param>
    public void Apply(EventCallback<bool> checkedChanged, EventCallback<ValueChangeEventArgs<bool>> onCheckedChange, bool disabled, bool highlighted)
    {
        this.checkedChanged = checkedChanged;
        this.onCheckedChange = onCheckedChange;
        itemContext.Update(Checked, disabled, highlighted);
    }

    /// <summary>Wraps the item's content in its indicator's cascade.</summary>
    /// <param name="content">The consumer's content.</param>
    /// <returns>The content to render inside the item.</returns>
    public RenderFragment? CascadeContent(RenderFragment? content) => itemContext.Cascade(content);

    /// <summary>Requests the toggle an activation causes.</summary>
    /// <param name="snapshot">The snapshot of the activating click.</param>
    /// <returns>A task that completes when the request has run.</returns>
    public async Task ToggleAsync(EventSnapshot snapshot)
    {
        bool next = !Checked;
        if (await state!.RequestAsync(next, new ChangeEventDetails(EventReason.ItemPress, snapshot), onCheckedChange, checkedChanged))
        {
            itemContext.Update(Checked, itemContext.Disabled, itemContext.Highlighted);
            requestRender();
        }
    }
}

// MNU-API-44..47, MNU-EVT-09 (menu/radio-group/MenuRadioGroup.tsx:25, 106-123; radio-item/MenuRadioItem.tsx:139-162): the
// RadioGroup<TValue> shape (ADR 08 Amendments 2026-11-03 (2), chosen by the user): Controlled<TValue>, items compared with
// Equality.StrictEquals<TValue>, OnValueChange also when the item is already selected (EVD-STA-09); no composite root of
// its own and no Field/Form wiring (MNU §8). Disabled is the group's, ORed into its items.
/// <summary>The behavior of a menu radio group's value.</summary>
/// <typeparam name="TValue">The type of the group's value.</typeparam>
internal sealed class MenuRadioGroupBehavior<TValue>
{
    private readonly string familyName;
    private readonly Action requestRender;
    private readonly RadioContext group;
    private readonly MenuGroupContext labelContext = new();
    private readonly RenderFragment<RenderFragment?> cascade;
    private Controlled<TValue>? state;
    private EventCallback<TValue> valueChanged;
    private EventCallback<ValueChangeEventArgs<TValue>> onValueChange;
    private bool disabled;

    /// <summary>Initializes a new instance of the <see cref="MenuRadioGroupBehavior{TValue}"/> class.</summary>
    /// <param name="familyName">The group's family, such as <c>Menu</c>.</param>
    /// <param name="requestRender">Asks the part to render.</param>
    public MenuRadioGroupBehavior(string familyName, Action requestRender)
    {
        this.familyName = familyName;
        this.requestRender = requestRender;
        group = new RadioContext(this);
        cascade = content => builder =>
        {
            builder.OpenComponent<CascadingValue<MenuRadioGroupContext<TValue>>>(0);
            builder.AddComponentParameter(1, nameof(CascadingValue<MenuRadioGroupContext<TValue>>.Value), group);
            builder.AddComponentParameter(2, nameof(CascadingValue<MenuRadioGroupContext<TValue>>.IsFixed), true);
            builder.AddComponentParameter(3, nameof(CascadingValue<MenuRadioGroupContext<TValue>>.ChildContent), labelContext.Cascade(content));
            builder.CloseComponent();
        };
        labelContext.Subscribe(requestRender);
    }

    /// <summary>Gets the context the group cascades to its items.</summary>
    public MenuRadioGroupContext<TValue> GroupContext => group;

    /// <summary>Gets the group context its group label reads.</summary>
    public MenuGroupContext LabelContext => labelContext;

    /// <summary>Reads whether <c>Value</c> was supplied, fixing control at the first call; call it from the part's <c>SetParametersAsync</c>.</summary>
    /// <param name="parameters">The part's parameters.</param>
    /// <param name="logger">The part's logger, which receives the controlled-state warnings.</param>
    public void SetParameters(ParameterView parameters, ILogger? logger)
    {
        state ??= new Controlled<TValue>(familyName + "RadioGroup", "value", logger);
        state.SetParameters(parameters, "Value", "DefaultValue");
    }

    /// <summary>Applies the group's callbacks and disabled state.</summary>
    /// <param name="valueChanged">The <c>ValueChanged</c> callback.</param>
    /// <param name="onValueChange">The <c>OnValueChange</c> callback.</param>
    /// <param name="disabled">The group's <c>Disabled</c>.</param>
    public void Apply(EventCallback<TValue> valueChanged, EventCallback<ValueChangeEventArgs<TValue>> onValueChange, bool disabled)
    {
        this.valueChanged = valueChanged;
        this.onValueChange = onValueChange;

        // The items render again only when what they read changed (a controlled value is read at their render).
        if (this.disabled != disabled)
        {
            this.disabled = disabled;
            group.Notify();
        }
    }

    /// <summary>Wraps the group's content in its items' and label's cascades.</summary>
    /// <param name="content">The consumer's content.</param>
    /// <returns>The content to render inside the group.</returns>
    public RenderFragment? CascadeContent(RenderFragment? content) => cascade(content);

    /// <summary>Adds the group's attributes.</summary>
    /// <param name="attributes">The part's attributes.</param>
    public void AddAttributes(AttributeList attributes)
    {
        attributes.Set("role", "group");
        if (labelContext.LabelId is { } labelId)
        {
            attributes.Set("aria-labelledby", labelId);
        }

        if (disabled)
        {
            attributes.Set("aria-disabled", "true");
        }
    }

    private sealed class RadioContext(MenuRadioGroupBehavior<TValue> owner) : MenuRadioGroupContext<TValue>
    {
        private readonly List<Action> listeners = [];

        /// <inheritdoc />
        public override bool Disabled => owner.disabled;

        /// <inheritdoc />
        public override bool IsSelected(TValue value) => owner.state is { } current && Equality.StrictEquals(current.Value, value);

        /// <inheritdoc />
        public override async Task SelectAsync(TValue value, EventSnapshot snapshot)
        {
            // EVD-STA-09: called even when the item is already selected.
            if (await owner.state!.RequestAsync(value, new ChangeEventDetails(EventReason.ItemPress, snapshot), owner.onValueChange, owner.valueChanged))
            {
                Notify();
                owner.requestRender();
            }
        }

        /// <inheritdoc />
        public override IDisposable Subscribe(Action changed)
        {
            listeners.Add(changed);
            return new MenuContextListener(listeners, changed);
        }

        public void Notify()
        {
            foreach (Action listener in listeners.ToArray())
            {
                listener();
            }
        }
    }
}

/// <summary>The state a menu radio group shares with its items.</summary>
/// <typeparam name="TValue">The type of the group's value.</typeparam>
internal abstract class MenuRadioGroupContext<TValue>
{
    /// <summary>Gets a value indicating whether the group is disabled.</summary>
    public abstract bool Disabled { get; }

    /// <summary>Determines whether the given value is the group's.</summary>
    /// <param name="value">An item's value.</param>
    /// <returns><see langword="true"/> when the values are strictly equal.</returns>
    public abstract bool IsSelected(TValue value);

    /// <summary>Requests the selection of an item's value.</summary>
    /// <param name="value">The item's value.</param>
    /// <param name="snapshot">The snapshot of the activating click.</param>
    /// <returns>A task that completes when the request has run.</returns>
    public abstract Task SelectAsync(TValue value, EventSnapshot snapshot);

    /// <summary>Subscribes an item to changes of the group's value or disabled state.</summary>
    /// <param name="changed">Called after a change.</param>
    /// <returns>The subscription; disposing it unsubscribes.</returns>
    public abstract IDisposable Subscribe(Action changed);
}

// MNU-API-47: the radio item's own behaviour beside MenuItemBehavior (the checked state the group's).
/// <summary>The behavior of a menu radio item's checked state.</summary>
/// <typeparam name="TValue">The type of the group's value.</typeparam>
internal sealed class MenuRadioItemBehavior<TValue> : IDisposable
{
    private readonly string familyName;
    private readonly Action requestRender;
    private readonly MenuCheckableItemContext itemContext = new();
    private MenuRadioGroupContext<TValue>? group;
    private IDisposable? subscription;
    private TValue value = default!;

    /// <summary>Initializes a new instance of the <see cref="MenuRadioItemBehavior{TValue}"/> class.</summary>
    /// <param name="familyName">The item's family, such as <c>Menu</c>.</param>
    /// <param name="requestRender">Asks the part to render.</param>
    public MenuRadioItemBehavior(string familyName, Action requestRender)
    {
        this.familyName = familyName;
        this.requestRender = requestRender;
    }

    /// <summary>Gets a value indicating whether the item is checked.</summary>
    public bool Checked => group?.IsSelected(value) ?? false;

    /// <summary>Gets a value indicating whether the group disables the item.</summary>
    public bool GroupDisabled => group?.Disabled ?? false;

    /// <summary>Gets the context the item cascades to its indicator.</summary>
    public MenuCheckableItemContext ItemContext => itemContext;

    /// <inheritdoc />
    public void Dispose() => subscription?.Dispose();

    /// <summary>Checks the part's placement and subscribes to the group, once.</summary>
    /// <param name="group">The enclosing group's context, or <see langword="null"/>.</param>
    /// <exception cref="InvalidOperationException">The part is outside a radio group.</exception>
    public void Subscribe(MenuRadioGroupContext<TValue>? group)
    {
        this.group = group ?? throw MenuParts.MissingRadioGroup(familyName);
        subscription = group.Subscribe(requestRender);
    }

    /// <summary>Applies the item's value and the item's state its indicator reads.</summary>
    /// <param name="value">The item's <c>Value</c>.</param>
    /// <param name="disabled">The item's <c>Disabled</c>.</param>
    /// <param name="highlighted">Whether the item is highlighted.</param>
    public void Apply(TValue value, bool disabled, bool highlighted)
    {
        this.value = value;
        itemContext.Update(Checked, disabled, highlighted);
    }

    /// <summary>Wraps the item's content in its indicator's cascade.</summary>
    /// <param name="content">The consumer's content.</param>
    /// <returns>The content to render inside the item.</returns>
    public RenderFragment? CascadeContent(RenderFragment? content) => itemContext.Cascade(content);

    /// <summary>Requests the selection an activation causes.</summary>
    /// <param name="snapshot">The snapshot of the activating click.</param>
    /// <returns>A task that completes when the request has run.</returns>
    public Task SelectAsync(EventSnapshot snapshot) => group!.SelectAsync(value, snapshot);
}

// MenuCheckboxItemContext.ts, MenuRadioItemContext.ts: what an indicator reads from its item.
/// <summary>The state a menu checkbox or radio item shares with its indicator.</summary>
internal sealed class MenuCheckableItemContext
{
    private readonly List<Action> listeners = [];
    private RenderFragment? content;
    private RenderFragment? cascade;

    /// <summary>Gets a value indicating whether the item is checked.</summary>
    public bool Checked { get; private set; }

    /// <summary>Gets a value indicating whether the item is disabled.</summary>
    public bool Disabled { get; private set; }

    /// <summary>Gets a value indicating whether the item is highlighted.</summary>
    public bool Highlighted { get; private set; }

    /// <summary>Subscribes an indicator to changes of the item's state.</summary>
    /// <param name="changed">Called after a change.</param>
    /// <returns>The subscription; disposing it unsubscribes.</returns>
    public IDisposable Subscribe(Action changed)
    {
        listeners.Add(changed);
        return new MenuContextListener(listeners, changed);
    }

    /// <summary>Records the item's state and tells the indicator when it changed.</summary>
    /// <param name="isChecked">Whether the item is checked.</param>
    /// <param name="disabled">Whether the item is disabled.</param>
    /// <param name="highlighted">Whether the item is highlighted.</param>
    public void Update(bool isChecked, bool disabled, bool highlighted)
    {
        if (Checked == isChecked && Disabled == disabled && Highlighted == highlighted)
        {
            return;
        }

        Checked = isChecked;
        Disabled = disabled;
        Highlighted = highlighted;
        foreach (Action listener in listeners.ToArray())
        {
            listener();
        }
    }

    /// <summary>Wraps an item's content in the indicator's cascade.</summary>
    /// <param name="content">The consumer's content.</param>
    /// <returns>The content to render inside the item.</returns>
    public RenderFragment? Cascade(RenderFragment? content)
    {
        if (cascade is null || !ReferenceEquals(this.content, content))
        {
            this.content = content;
            cascade = builder =>
            {
                builder.OpenComponent<CascadingValue<MenuCheckableItemContext>>(0);
                builder.AddComponentParameter(1, nameof(CascadingValue<MenuCheckableItemContext>.Value), this);
                builder.AddComponentParameter(2, nameof(CascadingValue<MenuCheckableItemContext>.IsFixed), true);
                builder.AddComponentParameter(3, nameof(CascadingValue<MenuCheckableItemContext>.ChildContent), this.content);
                builder.CloseComponent();
            };
        }

        return cascade;
    }
}

// MNU-API-43, MNU-ANI-13 (checkbox-item-indicator/MenuCheckboxItemIndicator.tsx:2-9, 21, 70): KeepMounted and a transitions
// handle of its own on `checked` (Base UI's own, not RND-STR-10's): the indicator stays mounted through its exit, which the
// module reports. Both indicators, and ContextMenu's (track 7), are shells over it.
/// <summary>The behavior of a menu item indicator.</summary>
internal sealed class MenuItemIndicatorBehavior : IAsyncDisposable
{
    private const string OpenSelector = "[data-checked]";

    private readonly string familyName;
    private readonly IJSRuntime jsRuntime;
    private readonly Action requestRender;
    private readonly Func<Func<Task>, Task> invokeAsync;
    private MenuCheckableItemContext? item;
    private IDisposable? subscription;
    private TransitionsTwin? transitions;
    private TwinHandle<TransitionsOptions>? handle;
    private TransitionsOptions? sent;
    private Task? attaching;
    private bool keepMounted;
    private bool mounted;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="MenuItemIndicatorBehavior"/> class.</summary>
    /// <param name="familyName">The indicator's family, such as <c>Menu</c>.</param>
    /// <param name="jsRuntime">The runtime the twin imports its module through.</param>
    /// <param name="requestRender">Asks the part to render.</param>
    /// <param name="invokeAsync">Runs work on the part's renderer.</param>
    public MenuItemIndicatorBehavior(string familyName, IJSRuntime jsRuntime, Action requestRender, Func<Func<Task>, Task> invokeAsync)
    {
        this.familyName = familyName;
        this.jsRuntime = jsRuntime;
        this.requestRender = requestRender;
        this.invokeAsync = invokeAsync;
    }

    /// <summary>Gets the item's state.</summary>
    public MenuCheckableItemContext Item => item!;

    /// <summary>Gets the transition status the indicator renders.</summary>
    public TransitionStatus? TransitionStatus { get; private set; }

    /// <summary>Gets a value indicating whether the indicator renders its element: checked, kept mounted, or exiting.</summary>
    public bool Rendered => keepMounted || item!.Checked || mounted;

    /// <summary>Attaches the transitions handle after the render.</summary>
    /// <param name="element">The indicator's element.</param>
    /// <returns>A task that completes when the handle has its options.</returns>
    public async Task OnAfterRenderAsync(ElementReference? element)
    {
        if (element is not { } target || disposed)
        {
            return;
        }

        if (transitions is null)
        {
            transitions = new TransitionsTwin(jsRuntime);
            transitions.StatusChanged += change =>
            {
                TransitionStatus = change.Status;
                requestRender();
            };
            transitions.TransitionCompleted += opened => _ = invokeAsync(() =>
            {
                if (!opened && !item!.Checked)
                {
                    mounted = false;
                    requestRender();
                }

                return Task.CompletedTask;
            });
        }

        if (handle is null)
        {
            attaching ??= AttachAsync(target, transitions);
            await attaching;
        }
        else if (Options() is { } options && options != sent)
        {
            sent = options;
            await handle.UpdateAsync(options);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        disposed = true;
        subscription?.Dispose();
        if (transitions is not null)
        {
            await transitions.DisposeAsync();
        }
    }

    /// <summary>Checks the part's placement and subscribes to its item, once.</summary>
    /// <param name="item">The enclosing item's context, or <see langword="null"/>.</param>
    /// <param name="radio">Whether the indicator is a radio item's, which picks the message.</param>
    /// <exception cref="InvalidOperationException">The part is outside its item.</exception>
    public void Subscribe(MenuCheckableItemContext? item, bool radio)
    {
        this.item = item ?? throw (radio ? MenuParts.MissingRadioItem(familyName) : MenuParts.MissingCheckboxItem(familyName));
        mounted = item.Checked;
        subscription = item.Subscribe(() =>
        {
            mounted |= this.item.Checked;
            requestRender();
        });
    }

    /// <summary>Applies the indicator's <c>KeepMounted</c>.</summary>
    /// <param name="keepMounted">The parameter.</param>
    public void Apply(bool keepMounted) => this.keepMounted = keepMounted;

    /// <summary>Adds the indicator's attributes.</summary>
    /// <param name="attributes">The part's attributes.</param>
    public void AddAttributes(AttributeList attributes) => attributes.Set("aria-hidden", "true");

    private TransitionsOptions Options() => new()
    {
        Open = item!.Checked,
        Mounted = Rendered,
        Status = TransitionStatus,
        KeepMounted = keepMounted,
        ReportOpenComplete = true,
        OpenSelector = OpenSelector,
    };

    private async Task AttachAsync(ElementReference target, TransitionsTwin twin)
    {
        await twin.PrepareAsync();
        if (disposed || transitions != twin)
        {
            return;
        }

        TransitionsOptions options = Options();
        sent = options;
        handle = await twin.AttachAsync(target, options);
        attaching = null;
    }
}

// MNU-API-48, MNU-ARIA-10/11 (group/MenuGroup.tsx, group-label/MenuGroupLabel.tsx): the label's id, synced into its group's
// aria-labelledby; a label's cleanup clears the id only while it is still that label's (MenuGroupLabel.tsx:25-30).
/// <summary>The state a menu group or radio group shares with its label.</summary>
internal sealed class MenuGroupContext
{
    private readonly List<Action> listeners = [];
    private RenderFragment? content;
    private RenderFragment? cascade;

    /// <summary>Gets the id of the group's label, or <see langword="null"/>.</summary>
    public string? LabelId { get; private set; }

    /// <summary>Wraps a group's content in the label's cascade.</summary>
    /// <param name="content">The consumer's content.</param>
    /// <returns>The content to render inside the group.</returns>
    public RenderFragment? Cascade(RenderFragment? content)
    {
        if (cascade is null || !ReferenceEquals(this.content, content))
        {
            this.content = content;
            cascade = builder =>
            {
                builder.OpenComponent<CascadingValue<MenuGroupContext>>(0);
                builder.AddComponentParameter(1, nameof(CascadingValue<MenuGroupContext>.Value), this);
                builder.AddComponentParameter(2, nameof(CascadingValue<MenuGroupContext>.IsFixed), true);
                builder.AddComponentParameter(3, nameof(CascadingValue<MenuGroupContext>.ChildContent), this.content);
                builder.CloseComponent();
            };
        }

        return cascade;
    }

    /// <summary>Records the label's id and tells the group.</summary>
    /// <param name="id">The label's id, or <see langword="null"/> when it went away.</param>
    /// <param name="previous">When <paramref name="id"/> is <see langword="null"/>, the id of the label that went away: the id is cleared only while it is still that one.</param>
    public void SetLabelId(string? id, string? previous = null)
    {
        if (id is null && !string.Equals(LabelId, previous, StringComparison.Ordinal))
        {
            return;
        }

        if (string.Equals(LabelId, id, StringComparison.Ordinal))
        {
            return;
        }

        LabelId = id;
        foreach (Action listener in listeners.ToArray())
        {
            listener();
        }
    }

    /// <summary>Subscribes the group to changes of its label's id.</summary>
    /// <param name="changed">Called after a change.</param>
    /// <returns>The subscription; disposing it unsubscribes.</returns>
    public IDisposable Subscribe(Action changed)
    {
        listeners.Add(changed);
        return new MenuContextListener(listeners, changed);
    }
}

/// <summary>A listener registration of a menu part's context, removed on dispose.</summary>
/// <param name="listeners">The context's listeners.</param>
/// <param name="listener">The registered listener.</param>
internal sealed class MenuContextListener(List<Action> listeners, Action listener) : IDisposable
{
    /// <inheritdoc />
    public void Dispose() => listeners.Remove(listener);
}
