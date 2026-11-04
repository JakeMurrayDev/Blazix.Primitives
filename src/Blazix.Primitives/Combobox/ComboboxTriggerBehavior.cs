using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Blazix.Primitives;

// combobox/trigger/ComboboxTrigger.tsx (CMB §3.9, §5.3): a native button by default (ButtonBehavior), tabindex 0 inside /
// -1 outside, role="combobox" with the input inside, aria-haspopup dialog/listbox, aria-expanded. Its handles:
// - `interactions` trigger role, Click { Event = "mousedown" } (CMB-PTR-02), the consumer's onmousedown declined
//   (InteractionsTriggerBehavior's veto; ADR 01's "C#-origin consumer vetoes");
// - `composite` list handle: Reference with the input inside, else Trigger; the closed typeahead (CMB-KEY-12);
// - `combobox` `trigger`: the mousedown default and the one-shot mouseup (`cancel-open`, CMB-PTR-03).
/// <summary>The behavior of a combobox-family trigger: its button, its press, its list handle and its ARIA.</summary>
internal sealed class ComboboxTriggerBehavior : IAsyncDisposable
{
    /// <summary>Initializes a new instance of the <see cref="ComboboxTriggerBehavior"/> class.</summary>
    /// <param name="services">The part's services.</param>
    /// <param name="developmentLog">The button mechanism's development log.</param>
    /// <param name="onKeyDown">The part's own <c>onkeydown</c> handler.</param>
    /// <param name="onKeyUp">The part's own <c>onkeyup</c> handler.</param>
    /// <param name="onClick">The part's own <c>onclick</c> handler, which calls <see cref="HandleClickAsync"/>.</param>
    /// <param name="onMouseDown">The part's own <c>onmousedown</c> handler, which calls <see cref="HandleMouseDownAsync"/>.</param>
    public ComboboxTriggerBehavior(ComboboxPartServices services, ButtonDevelopmentLog developmentLog, EventCallback onKeyDown, EventCallback onKeyUp, EventCallback onClick, EventCallback onMouseDown) =>
        throw new NotImplementedException();

    /// <summary>Gets the root's store.</summary>
    public ComboboxStore Store => throw new NotImplementedException();

    /// <summary>Gets the field's state the trigger projects.</summary>
    public FieldStateSnapshot Field => throw new NotImplementedException();

    /// <summary>Gets a value indicating whether the trigger is disabled, by the part, the root or the field.</summary>
    public bool Disabled => throw new NotImplementedException();

    /// <summary>Gets a task that completes once the trigger's handles have attached.</summary>
    public Task TwinAttached => throw new NotImplementedException();

    /// <inheritdoc />
    public ValueTask DisposeAsync() => throw new NotImplementedException();

    /// <summary>Reads the root and the field and subscribes the part.</summary>
    /// <param name="store">The cascaded store, or <see langword="null"/> outside a root.</param>
    /// <param name="field">The cascaded field, or <see langword="null"/>.</param>
    /// <param name="subscribe">The part's subscription factory over the store.</param>
    public void Subscribe(ComboboxStore? store, FieldRootContext? field, Func<ComboboxStore, SelectorSubscription<ComboboxStoreState>> subscribe) =>
        throw new NotImplementedException();

    /// <summary>Applies the part's parameters.</summary>
    /// <param name="nativeButton">The part's <c>NativeButton</c>.</param>
    /// <param name="disabled">The part's <c>Disabled</c>.</param>
    /// <param name="id">The id the part renders.</param>
    /// <param name="consumerAttributes">The consumer's attributes.</param>
    public void Apply(bool nativeButton, bool disabled, string? id, IReadOnlyDictionary<string, object>? consumerAttributes) => throw new NotImplementedException();

    /// <summary>Adds the trigger's attributes.</summary>
    /// <param name="attributes">The part's attributes.</param>
    public void AddPartAttributes(AttributeList attributes) => throw new NotImplementedException();

    /// <summary>Determines whether the consumer's handler for an event renders on the element.</summary>
    /// <param name="eventName">The event's attribute name.</param>
    /// <returns><see langword="false"/> for a handler the part declines and runs itself.</returns>
    public bool ForwardsConsumerHandler(string eventName) => throw new NotImplementedException();

    /// <summary>Runs the declined consumer <c>onclick</c> and the button's click.</summary>
    /// <param name="args">The click.</param>
    /// <returns>A task that completes when the handlers have run.</returns>
    public Task HandleClickAsync(MouseEventArgs args) => throw new NotImplementedException();

    /// <summary>Runs the declined consumer <c>onmousedown</c> inside the press decision.</summary>
    /// <param name="args">The mousedown.</param>
    /// <returns>A task that completes when the handler has run.</returns>
    public Task HandleMouseDownAsync(MouseEventArgs args) => throw new NotImplementedException();

    /// <summary>Attaches the handles and sends their changed options.</summary>
    /// <param name="element">The trigger's element.</param>
    /// <returns>A task that completes when the handles have their options.</returns>
    public Task AfterRenderAsync(ElementReference? element) => throw new NotImplementedException();
}
