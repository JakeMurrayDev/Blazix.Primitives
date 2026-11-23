using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// The close button every dialog family composes (dialog/close/DialogClose.tsx; ADR 08 Amendments 2026-10-27): ButtonBehavior
// plus the part's own C# onclick, the CollapsibleTrigger shape (ADR 04 §1 gives `interactions` press-to-toggle only, and a
// close does not toggle). The consumer's onclick runs first through the pipeline's chain and may veto with
// PreventPartHandler() on both modes (DLG-PTR-19, RND-EVT-02/03). While open, a click requests a close with the reason
// close-press and the click's snapshot (ADR 04 Amendments 2026-10-21); while closed it requests nothing; disabled, the button
// mechanism blocks the click (DLG-API-23, UBT-PTR-01).
/// <summary>The behavior of a dialog-family close button.</summary>
internal sealed class DialogCloseBehavior : IDisposable
{
    private readonly ButtonBehavior button;
    private IDialogRootContext? context;

    /// <summary>Initializes a new instance of the <see cref="DialogCloseBehavior"/> class.</summary>
    /// <param name="jsRuntime">The runtime the button's twin imports its module through.</param>
    /// <param name="logger">The part's logger.</param>
    /// <param name="developmentLog">The button mechanism's development log.</param>
    /// <param name="idGenerator">The scoped id generator.</param>
    /// <param name="requestRender">Asks the part to render.</param>
    /// <param name="dispatchException">Routes an exception to the part.</param>
    /// <param name="onKeyDown">The part's own <c>onkeydown</c> handler.</param>
    /// <param name="onKeyUp">The part's own <c>onkeyup</c> handler.</param>
    public DialogCloseBehavior(IJSRuntime jsRuntime, ILogger logger, ButtonDevelopmentLog developmentLog, PartIdGenerator idGenerator, Action requestRender, Func<Exception, Task> dispatchException, EventCallback onKeyDown, EventCallback onKeyUp)
    {
        button = new ButtonBehavior(jsRuntime, logger, developmentLog, idGenerator, requestRender, dispatchException, onKeyDown, onKeyUp);
    }

    /// <summary>Gets a task that completes once the button's handle has attached.</summary>
    public Task TwinAttached => button.TwinAttached;

    /// <summary>Attaches the button's handle after the render.</summary>
    /// <param name="enabled">Whether the part rendered its element.</param>
    /// <param name="element">The element, or <see langword="null"/> under a substitute callback.</param>
    /// <param name="id">The id the part rendered.</param>
    /// <param name="disabled">The part's <c>Disabled</c>.</param>
    /// <returns>A task that completes when the handle has its options.</returns>
    public Task OnAfterRenderAsync(bool enabled, ElementReference? element, string? id, bool disabled)
    {
        button.Disabled = disabled;
        return button.AfterRenderAsync(enabled, element, id, composite: false);
    }

    /// <inheritdoc />
    public void Dispose() => button.Dispose();

    /// <summary>Configures the button at each parameter set.</summary>
    /// <param name="rootContext">The enclosing root's context.</param>
    /// <param name="nativeButton">The part's <c>NativeButton</c>.</param>
    /// <param name="disabled">The part's <c>Disabled</c>.</param>
    /// <param name="additionalAttributes">The part's unmatched attributes.</param>
    public void Apply(IDialogRootContext rootContext, bool nativeButton, bool disabled, IReadOnlyDictionary<string, object>? additionalAttributes)
    {
        context = rootContext;
        button.Native = nativeButton;
        button.FocusableWhenDisabled = null;
        button.Disabled = disabled;
        button.SetConsumerAttributes(additionalAttributes);
    }

    /// <summary>Adds the button's attributes and the part's <c>onclick</c>.</summary>
    /// <param name="attributes">The part's attributes.</param>
    /// <param name="id">The id the part renders.</param>
    /// <param name="disabled">The part's <c>Disabled</c>.</param>
    /// <param name="onClick">The part's own <c>onclick</c> handler.</param>
    public void AddAttributes(AttributeList attributes, string? id, bool disabled, EventCallback onClick)
    {
        button.Disabled = disabled;
        attributes.Set("onclick", onClick);
        button.AddAttributes(attributes, id, composite: false);
    }

    /// <summary>Determines whether the consumer's handler for an event is rendered on the element.</summary>
    /// <param name="eventName">The event's attribute name in lowercase.</param>
    /// <returns><see langword="false"/> for a handler the button runs or withholds; otherwise <see langword="true"/>.</returns>
    public bool ForwardsConsumerHandler(string eventName) => button.ForwardsConsumerHandler(eventName);

    /// <summary>Requests the close of an open dialog, after the consumer's <c>onclick</c> did not veto it.</summary>
    /// <param name="e">The click's arguments.</param>
    /// <param name="disabled">The part's <c>Disabled</c>.</param>
    /// <returns>A task that completes when the request has run.</returns>
    public Task HandleClickAsync(MouseEventArgs e, bool disabled)
    {
        if (disabled || context is not { Open: true } root)
        {
            return Task.CompletedTask;
        }

        return root.RequestOpenAsync(false, new OpenChangeEventDetails(EventReason.ClosePress, EventSnapshot.FromEventArgs(e)));
    }

    /// <summary>Runs the consumer's <c>onkeydown</c> through the button.</summary>
    /// <param name="e">The event arguments.</param>
    /// <returns>The consumer handler's task.</returns>
    public Task HandleKeyDownAsync(KeyboardEventArgs e) => button.HandleKeyDownAsync(e);

    /// <summary>Runs the consumer's <c>onkeyup</c> through the button.</summary>
    /// <param name="e">The event arguments.</param>
    /// <returns>The consumer handler's task.</returns>
    public Task HandleKeyUpAsync(KeyboardEventArgs e) => button.HandleKeyUpAsync(e);
}
