using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Rendering;

// RND-ALG-04 as settled by ADR 02 decision 3
/// <summary>Runs attribute handlers in precedence order, stopping when a handler vetoes the rest.</summary>
/// <remarks>
/// Each handler is invoked as Blazor's renderer would invoke it. After each one, if the argument is an
/// <see cref="EventArgs"/> on which <see cref="EventArgsVetoExtensions.PreventPartHandler"/> was called, the rest of the
/// chain is skipped. When the argument is the shared <see cref="EventArgs.Empty"/> instance, its veto is cleared after the
/// chain. Shared by <see cref="MergeProps"/> (which closes over two handlers) and the render pipeline (substitute,
/// consumer and part handlers).
/// </remarks>
internal static class HandlerChain
{
    /// <summary>Runs up to three handlers in order, stopping at a veto.</summary>
    /// <param name="arg">The event argument passed to each handler.</param>
    /// <param name="first">The handler that runs first, or a value that is not a handler.</param>
    /// <param name="second">The handler that runs second, or a value that is not a handler.</param>
    /// <param name="third">The handler that runs third, or a value that is not a handler.</param>
    /// <returns>A task that completes when the chain has run.</returns>
    public static async Task InvokeAsync(object? arg, AttributeValue first, AttributeValue second, AttributeValue third = default)
    {
        EventArgs? e = arg as EventArgs;
        try
        {
            if (first.IsHandler)
            {
                await first.InvokeAsync(arg).ConfigureAwait(false);
                if (Vetoed(e))
                {
                    return;
                }
            }

            if (second.IsHandler)
            {
                await second.InvokeAsync(arg).ConfigureAwait(false);
                if (Vetoed(e))
                {
                    return;
                }
            }

            if (third.IsHandler)
            {
                await third.InvokeAsync(arg).ConfigureAwait(false);
            }
        }
        finally
        {
            if (ReferenceEquals(e, EventArgs.Empty))
            {
                e.ClearPartHandlerVeto();
            }
        }
    }

    // RND-EVT-02
    /// <summary>
    /// Runs a part's chain: the substitute's handler, then the consumer's, then the part's typed handler, stopping at a veto.
    /// </summary>
    /// <typeparam name="TArgs">The event argument type.</typeparam>
    /// <param name="args">The event argument passed to each handler.</param>
    /// <param name="substitute">The substitute element's handler, or a value that is not a handler.</param>
    /// <param name="consumer">The consumer's handler, or a value that is not a handler.</param>
    /// <param name="part">The part's typed handler, which runs last.</param>
    /// <returns>A task that completes when the chain has run.</returns>
    public static async Task InvokeAsync<TArgs>(TArgs args, AttributeValue substitute, AttributeValue consumer, Func<TArgs, Task> part)
        where TArgs : EventArgs
    {
        try
        {
            if (substitute.IsHandler)
            {
                await substitute.InvokeAsync(args).ConfigureAwait(false);
                if (Vetoed(args))
                {
                    return;
                }
            }

            if (consumer.IsHandler)
            {
                await consumer.InvokeAsync(args).ConfigureAwait(false);
                if (Vetoed(args))
                {
                    return;
                }
            }

            await part(args).ConfigureAwait(false);
        }
        finally
        {
            if (ReferenceEquals(args, EventArgs.Empty))
            {
                args.ClearPartHandlerVeto();
            }
        }
    }

    // RND-EVT-02
    /// <summary>
    /// Combines <paramref name="first"/> and <paramref name="second"/> into one callback value that runs them in that order;
    /// this is the public merge's chain.
    /// </summary>
    /// <param name="first">The handler that runs first.</param>
    /// <param name="second">The handler that runs second.</param>
    /// <returns>
    /// <paramref name="first"/> when <paramref name="second"/> is not a handler; <paramref name="second"/> when
    /// <paramref name="first"/> is not a handler; otherwise, a new callback that runs both, stopping at a veto.
    /// </returns>
    public static AttributeValue Chain(AttributeValue first, AttributeValue second)
    {
        if (!second.IsHandler)
        {
            return first;
        }

        if (!first.IsHandler)
        {
            return second;
        }

        return new AttributeValue(new EventCallback(null, new Func<EventArgs?, Task>(e => InvokeAsync(e, first, second))));
    }

    private static bool Vetoed(EventArgs? e) => e is not null && e.PartHandlerPrevented();
}
