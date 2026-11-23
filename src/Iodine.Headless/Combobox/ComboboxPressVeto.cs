using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Blazix.Primitives;

// ADR 04 Amendments 2026-11-10 (design point 6, chosen by the user): the consumer's onmousedown on the Chips and the
// InputGroup runs before the press (handleInputPress.ts:9), and its PreventPartHandler() skips it. The part declines the
// handler and runs it from its own; on WebAssembly the `press` point runs it inside the dispatch, and Blazor's later
// dispatch of that mousedown skips it (the Label's one-shot marker); on Server the point falls back to `run` and the handler
// runs from Blazor's dispatch, after the press.
/// <summary>The consumer's <c>onmousedown</c> veto of a chips or input group press, the <c>press</c> decision point.</summary>
internal sealed class ComboboxPressVeto(Func<Exception, Task> dispatchException)
{
    private const string MouseDownEvent = "onmousedown";

    private readonly FieldConsumerHandlers handlers = new(MouseDownEvent);

    private DecidedPointer? decided;

    /// <summary>Gets a value indicating whether the consumer has an <c>onmousedown</c>, so the module asks <c>press</c>.</summary>
    public bool Decides => handlers.Has(MouseDownEvent);

    /// <summary>Takes the consumer's handler from the part's attributes.</summary>
    /// <param name="consumerAttributes">The consumer's attributes.</param>
    public void Set(IReadOnlyDictionary<string, object>? consumerAttributes) => handlers.Set(consumerAttributes);

    /// <summary>Determines whether the consumer's handler for an event renders on the element.</summary>
    /// <param name="eventName">The event's attribute name.</param>
    /// <returns><see langword="false"/> for the <c>onmousedown</c> the part runs itself.</returns>
    public bool ForwardsConsumerHandler(string eventName) => !handlers.Declines(eventName);

    /// <summary>Adds the part's own <c>onmousedown</c> while the consumer has one.</summary>
    /// <param name="attributes">The part's attributes.</param>
    /// <param name="onMouseDown">The part's own handler.</param>
    public void AddAttributes(AttributeList attributes, EventCallback onMouseDown)
    {
        if (Decides)
        {
            attributes.Set(MouseDownEvent, onMouseDown);
        }
    }

    /// <summary>Answers the <c>press</c> point: runs the consumer's handler inside the dispatch.</summary>
    /// <param name="point">The point the module asked.</param>
    /// <param name="snapshot">The mousedown's snapshot.</param>
    /// <returns><see cref="DecisionPolicy.Stop"/> when the handler called <c>PreventPartHandler()</c>.</returns>
    public DecisionPolicy Decide(string point, EventSnapshot snapshot)
    {
        if (!string.Equals(point, ComboboxTwin.PressPoint, StringComparison.Ordinal) || !Decides)
        {
            return DecisionPolicy.Run;
        }

        MouseEventArgs args = FieldConsumerHandlers.ToMouseEventArgs(snapshot);
        decided = DecidedPointer.From(args);
        return handlers.RunSynchronously(MouseDownEvent, args, dispatchException) ? DecisionPolicy.Stop : DecisionPolicy.Run;
    }

    /// <summary>Runs the consumer's handler from the part's own, unless the <c>press</c> point already ran it for this event.</summary>
    /// <param name="args">The mousedown.</param>
    /// <returns>A task that completes when the handler has run.</returns>
    public Task RunConsumerAsync(MouseEventArgs args)
    {
        DecidedPointer? pending = decided;
        decided = null;
        if (pending is { } previous && previous == DecidedPointer.From(args))
        {
            return Task.CompletedTask;
        }

        return handlers.RunAsync(MouseDownEvent, args);
    }

    private readonly record struct DecidedPointer(string Type, long Detail, long Buttons, bool AltKey, bool CtrlKey, bool MetaKey, bool ShiftKey)
    {
        public static DecidedPointer From(MouseEventArgs e) => new(e.Type, e.Detail, e.Buttons, e.AltKey, e.CtrlKey, e.MetaKey, e.ShiftKey);
    }
}
