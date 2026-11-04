using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// MNU-API-41/42, MNU-EVT-08 (menu/checkbox-item/MenuCheckboxItem.tsx:26-29, 112, 165-200): the checked state is held by the
// part, above its element (Controlled<bool>, fixed at the first parameter set), toggled by the item's activation; the
// change is cancelable. ContextMenuCheckboxItem (track 7) is a shell over it.
/// <summary>The behavior of a menu checkbox item's checked state.</summary>
internal sealed class MenuCheckboxItemBehavior
{
    /// <summary>Initializes a new instance of the <see cref="MenuCheckboxItemBehavior"/> class.</summary>
    /// <param name="familyName">The item's family, such as <c>Menu</c>.</param>
    /// <param name="requestRender">Asks the part to render.</param>
    public MenuCheckboxItemBehavior(string familyName, Action requestRender) => throw new NotImplementedException();

    /// <summary>Gets a value indicating whether the item is checked.</summary>
    public bool Checked => throw new NotImplementedException();

    /// <summary>Gets the context the item cascades to its indicator.</summary>
    public MenuCheckableItemContext ItemContext => throw new NotImplementedException();

    /// <summary>Applies the checked parameters; control is fixed at the first call.</summary>
    /// <param name="parameters">The part's parameters, from which whether <c>Checked</c> was supplied is read.</param>
    /// <param name="value">The <c>Checked</c> parameter.</param>
    /// <param name="defaultValue">The <c>DefaultChecked</c> parameter.</param>
    /// <param name="valueChanged">The <c>CheckedChanged</c> callback.</param>
    /// <param name="onValueChange">The <c>OnCheckedChange</c> callback.</param>
    public void Apply(ParameterView parameters, bool value, bool defaultValue, EventCallback<bool> valueChanged, EventCallback<ValueChangeEventArgs<bool>> onValueChange) =>
        throw new NotImplementedException();

    /// <summary>Requests the toggle an activation causes.</summary>
    /// <param name="snapshot">The snapshot of the activating click.</param>
    /// <returns>A task that completes when the request has run.</returns>
    public Task ToggleAsync(EventSnapshot snapshot) => throw new NotImplementedException();
}

// MNU-API-44..47, MNU-EVT-09 (menu/radio-group/MenuRadioGroup.tsx:25, 106-123; radio-item/MenuRadioItem.tsx:139-162): the
// RadioGroup<TValue> shape (ADR 08 Amendments 2026-11-03 (2), chosen by the user): Controlled<TValue>, items compared with
// Equality.StrictEquals<TValue>, OnValueChange also when the item is already selected (EVD-STA-09); no composite root of
// its own and no Field/Form wiring (MNU §8). Disabled is the group's, ORed into its items.
/// <summary>The behavior of a menu radio group's value.</summary>
/// <typeparam name="TValue">The type of the group's value.</typeparam>
internal sealed class MenuRadioGroupBehavior<TValue>
{
    /// <summary>Initializes a new instance of the <see cref="MenuRadioGroupBehavior{TValue}"/> class.</summary>
    /// <param name="familyName">The group's family, such as <c>Menu</c>.</param>
    /// <param name="requestRender">Asks the part to render.</param>
    public MenuRadioGroupBehavior(string familyName, Action requestRender) => throw new NotImplementedException();

    /// <summary>Gets the context the group cascades to its items.</summary>
    public MenuRadioGroupContext<TValue> GroupContext => throw new NotImplementedException();

    /// <summary>Gets the group context its group label reads.</summary>
    public MenuGroupContext LabelContext => throw new NotImplementedException();

    /// <summary>Applies the group's parameters; control is fixed at the first call.</summary>
    /// <param name="parameters">The part's parameters, from which whether <c>Value</c> was supplied is read.</param>
    /// <param name="value">The <c>Value</c> parameter.</param>
    /// <param name="defaultValue">The <c>DefaultValue</c> parameter.</param>
    /// <param name="valueChanged">The <c>ValueChanged</c> callback.</param>
    /// <param name="onValueChange">The <c>OnValueChange</c> callback.</param>
    /// <param name="disabled">The group's <c>Disabled</c>.</param>
    public void Apply(ParameterView parameters, TValue value, TValue defaultValue, EventCallback<TValue> valueChanged, EventCallback<ValueChangeEventArgs<TValue>> onValueChange, bool disabled) =>
        throw new NotImplementedException();

    /// <summary>Adds the group's attributes.</summary>
    /// <param name="attributes">The part's attributes.</param>
    public void AddAttributes(AttributeList attributes) => throw new NotImplementedException();
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
    /// <summary>Initializes a new instance of the <see cref="MenuRadioItemBehavior{TValue}"/> class.</summary>
    /// <param name="familyName">The item's family, such as <c>Menu</c>.</param>
    /// <param name="requestRender">Asks the part to render.</param>
    public MenuRadioItemBehavior(string familyName, Action requestRender) => throw new NotImplementedException();

    /// <summary>Gets a value indicating whether the item is checked.</summary>
    public bool Checked => throw new NotImplementedException();

    /// <summary>Gets a value indicating whether the group disables the item.</summary>
    public bool GroupDisabled => throw new NotImplementedException();

    /// <summary>Gets the context the item cascades to its indicator.</summary>
    public MenuCheckableItemContext ItemContext => throw new NotImplementedException();

    /// <inheritdoc />
    public void Dispose() => throw new NotImplementedException();

    /// <summary>Checks the part's placement and subscribes to the group, once.</summary>
    /// <param name="group">The enclosing group's context, or <see langword="null"/>.</param>
    /// <exception cref="InvalidOperationException">The part is outside a radio group.</exception>
    public void Subscribe(MenuRadioGroupContext<TValue>? group) => throw new NotImplementedException();

    /// <summary>Applies the item's value.</summary>
    /// <param name="value">The item's <c>Value</c>.</param>
    public void Apply(TValue value) => throw new NotImplementedException();

    /// <summary>Requests the selection an activation causes.</summary>
    /// <param name="snapshot">The snapshot of the activating click.</param>
    /// <returns>A task that completes when the request has run.</returns>
    public Task SelectAsync(EventSnapshot snapshot) => throw new NotImplementedException();
}

// MenuCheckboxItemContext.ts, MenuRadioItemContext.ts: what an indicator reads from its item.
/// <summary>The state a menu checkbox or radio item shares with its indicator.</summary>
internal sealed class MenuCheckableItemContext
{
    /// <summary>Gets a value indicating whether the item is checked.</summary>
    public bool Checked { get; internal set; }

    /// <summary>Gets a value indicating whether the item is disabled.</summary>
    public bool Disabled { get; internal set; }

    /// <summary>Gets a value indicating whether the item is highlighted.</summary>
    public bool Highlighted { get; internal set; }

    /// <summary>Subscribes an indicator to changes of the item's state.</summary>
    /// <param name="changed">Called after a change.</param>
    /// <returns>The subscription; disposing it unsubscribes.</returns>
    public IDisposable Subscribe(Action changed) => throw new NotImplementedException();
}

// MNU-API-43, MNU-ANI-13 (checkbox-item-indicator/MenuCheckboxItemIndicator.tsx:2-9, 21, 70): KeepMounted and a transitions
// handle of its own on `checked` (Base UI's own, not RND-STR-10's). Both indicators, and ContextMenu's (track 7), are shells
// over it.
/// <summary>The behavior of a menu item indicator.</summary>
internal sealed class MenuItemIndicatorBehavior : IAsyncDisposable
{
    /// <summary>Initializes a new instance of the <see cref="MenuItemIndicatorBehavior"/> class.</summary>
    /// <param name="familyName">The indicator's family, such as <c>Menu</c>.</param>
    /// <param name="jsRuntime">The runtime the twin imports its module through.</param>
    /// <param name="requestRender">Asks the part to render.</param>
    /// <param name="invokeAsync">Runs work on the part's renderer.</param>
    public MenuItemIndicatorBehavior(string familyName, IJSRuntime jsRuntime, Action requestRender, Func<Func<Task>, Task> invokeAsync) =>
        throw new NotImplementedException();

    /// <summary>Gets the item's state.</summary>
    public MenuCheckableItemContext Item => throw new NotImplementedException();

    /// <summary>Gets the transition status the indicator renders.</summary>
    public TransitionStatus? TransitionStatus => throw new NotImplementedException();

    /// <summary>Gets a value indicating whether the indicator renders its element: checked, kept mounted, or exiting.</summary>
    public bool Rendered => throw new NotImplementedException();

    /// <summary>Attaches the transitions handle after the render.</summary>
    /// <param name="element">The indicator's element.</param>
    /// <returns>A task that completes when the handle has its options.</returns>
    public Task OnAfterRenderAsync(ElementReference? element) => throw new NotImplementedException();

    /// <inheritdoc />
    public ValueTask DisposeAsync() => throw new NotImplementedException();

    /// <summary>Checks the part's placement and subscribes to its item, once.</summary>
    /// <param name="item">The enclosing item's context, or <see langword="null"/>.</param>
    /// <param name="radio">Whether the indicator is a radio item's, which picks the message.</param>
    /// <exception cref="InvalidOperationException">The part is outside its item.</exception>
    public void Subscribe(MenuCheckableItemContext? item, bool radio) => throw new NotImplementedException();

    /// <summary>Applies the indicator's <c>KeepMounted</c>.</summary>
    /// <param name="keepMounted">The parameter.</param>
    public void Apply(bool keepMounted) => throw new NotImplementedException();

    /// <summary>Adds the indicator's attributes.</summary>
    /// <param name="attributes">The part's attributes.</param>
    public void AddAttributes(AttributeList attributes) => throw new NotImplementedException();
}

// MNU-API-48, MNU-ARIA-10/11 (group/MenuGroup.tsx, group-label/MenuGroupLabel.tsx): the label's id, synced into its group's
// aria-labelledby (the PopoverIdSync form).
/// <summary>The state a menu group or radio group shares with its label.</summary>
internal sealed class MenuGroupContext
{
    /// <summary>Gets the id of the group's label, or <see langword="null"/>.</summary>
    public string? LabelId { get; private set; }

    /// <summary>Records the label's id and tells the group.</summary>
    /// <param name="id">The label's id, or <see langword="null"/> when it went away.</param>
    public void SetLabelId(string? id) => throw new NotImplementedException();

    /// <summary>Subscribes the group to changes of its label's id.</summary>
    /// <param name="changed">Called after a change.</param>
    /// <returns>The subscription; disposing it unsubscribes.</returns>
    public IDisposable Subscribe(Action changed) => throw new NotImplementedException();
}
