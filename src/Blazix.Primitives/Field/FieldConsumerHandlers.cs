using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace Blazix.Primitives;

// FLD-API-14, FLD-PTR-04, FLD-EVT-09, FLD-KEY-04 (EVD-API-11): a consumer handler runs before the part's own logic and may
// veto it with PreventPartHandler(). The part declines the handler (ForwardsConsumerHandler, ADR 02 Amendments 2026-10-21)
// and runs it itself: from a module report for an event the form module owns (on both render modes), or inside the label
// decision point on WebAssembly, with a one-shot marker so that Blazor's own dispatch of the same event does not run it a
// second time (the ButtonBehavior pattern, ADR 02 Amendments 2026-10-21; ADR 01's "C#-origin consumer vetoes" row).
/// <summary>The consumer handlers a field part declines from the element and runs itself, found once per parameter set.</summary>
internal sealed class FieldConsumerHandlers
{
    private readonly string[] names;
    private readonly AttributeValue[] handlers;

    /// <summary>Initializes a new instance of the <see cref="FieldConsumerHandlers"/> class.</summary>
    /// <param name="names">The lowercase event attribute names the part runs itself, such as <c>oninput</c>.</param>
    public FieldConsumerHandlers(params string[] names)
    {
        this.names = names;
        handlers = new AttributeValue[names.Length];
    }

    /// <summary>Gets a value indicating whether the consumer supplied a handler for any of the events.</summary>
    /// <value><see langword="true"/> when at least one handler was found.</value>
    public bool Any
    {
        get
        {
            foreach (AttributeValue handler in handlers)
            {
                if (handler.IsHandler)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>Finds the consumer's handlers in the part's unmatched attributes.</summary>
    /// <param name="additionalAttributes">The part's unmatched attributes.</param>
    public void Set(IReadOnlyDictionary<string, object>? additionalAttributes)
    {
        Array.Clear(handlers);
        if (additionalAttributes is null)
        {
            return;
        }

        foreach (KeyValuePair<string, object> pair in additionalAttributes)
        {
            int index = IndexOf(pair.Key);
            if (index < 0)
            {
                continue;
            }

            AttributeValue value = AttributeValue.FromObject(pair.Value);
            if (MergeProps.IsHandler(pair.Key, value))
            {
                handlers[index] = value;
            }
        }
    }

    /// <summary>Determines whether the part declines the consumer's handler for an event.</summary>
    /// <param name="eventName">The event's attribute name in lowercase.</param>
    /// <returns><see langword="true"/> for one of the events the part runs itself.</returns>
    public bool Declines(string eventName) => IndexOf(eventName) >= 0;

    /// <summary>Determines whether the consumer supplied a handler for an event.</summary>
    /// <param name="eventName">The event's attribute name in lowercase.</param>
    /// <returns><see langword="true"/> when a handler was found.</returns>
    public bool Has(string eventName) => IndexOf(eventName) is int index and >= 0 && handlers[index].IsHandler;

    /// <summary>Runs the consumer's handler for an event and reports its veto.</summary>
    /// <param name="eventName">The event's attribute name in lowercase.</param>
    /// <param name="args">The event arguments the handler receives.</param>
    /// <returns>
    /// A task whose result is <see langword="true"/> when the handler called <see cref="EventArgsVetoExtensions.PreventPartHandler"/>;
    /// <see langword="false"/> when it did not or there is no handler.
    /// </returns>
    public async Task<bool> RunAsync(string eventName, EventArgs args)
    {
        int index = IndexOf(eventName);
        if (index < 0 || !handlers[index].IsHandler)
        {
            return false;
        }

        await handlers[index].InvokeAsync(args).ConfigureAwait(true);
        return args.PartHandlerPrevented();
    }

    // Inside a decision only the synchronous part of the handler can veto (ADR 04 §3).
    /// <summary>Runs the consumer's handler for an event synchronously, as a decision point does.</summary>
    /// <param name="eventName">The event's attribute name in lowercase.</param>
    /// <param name="args">The event arguments the handler receives.</param>
    /// <param name="dispatchException">Routes an exception of the handler to the part.</param>
    /// <returns><see langword="true"/> when the handler called <see cref="EventArgsVetoExtensions.PreventPartHandler"/>.</returns>
    public bool RunSynchronously(string eventName, EventArgs args, Func<Exception, Task> dispatchException)
    {
        int index = IndexOf(eventName);
        if (index < 0 || !handlers[index].IsHandler)
        {
            return false;
        }

        try
        {
            Task task = handlers[index].InvokeAsync(args);
            if (task.IsFaulted)
            {
                _ = dispatchException(task.Exception!.GetBaseException());
            }
            else if (!task.IsCompleted)
            {
                _ = ObserveAsync(task, dispatchException);
            }
        }
        catch (Exception exception)
        {
            _ = dispatchException(exception);
        }

        return args.PartHandlerPrevented();
    }

    // MouseEventArgs and PointerEventArgs as Blazor builds them, from the snapshot the module took inside the dispatch.
    /// <summary>Builds the mouse or pointer event arguments of a snapshot.</summary>
    /// <param name="snapshot">The event's snapshot.</param>
    /// <returns>A <see cref="PointerEventArgs"/> for a pointer event; otherwise, a <see cref="MouseEventArgs"/>.</returns>
    public static MouseEventArgs ToMouseEventArgs(EventSnapshot snapshot)
    {
        MouseEventArgs args = snapshot.Kind == EventKind.PointerEvent ? new PointerEventArgs { PointerType = snapshot.PointerType ?? string.Empty } : new MouseEventArgs();
        args.Type = snapshot.Type;
        args.Detail = snapshot.Detail;
        args.Buttons = snapshot.Buttons;
        args.AltKey = snapshot.AltKey;
        args.CtrlKey = snapshot.CtrlKey;
        args.MetaKey = snapshot.MetaKey;
        args.ShiftKey = snapshot.ShiftKey;
        return args;
    }

    private static async Task ObserveAsync(Task task, Func<Exception, Task> dispatchException)
    {
        try
        {
            await task.ConfigureAwait(true);
        }
        catch (Exception exception) when (!task.IsCanceled)
        {
            await dispatchException(exception).ConfigureAwait(true);
        }
    }

    private int IndexOf(string name)
    {
        for (int i = 0; i < names.Length; i++)
        {
            if (string.Equals(names[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }
}
