using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Rendering;

/// <summary>
/// Runs handlers in precedence order with the veto (RND-ALG-04 as settled by ADR 02 decision 3): each handler is
/// invoked as Blazor's renderer would invoke it; after each one, if the argument is an <see cref="EventArgs"/> on
/// which <see cref="EventArgsVetoExtensions.PreventPartHandler"/> was called, the rest of the chain is skipped.
/// Shared by <see cref="MergeProps"/> (which closes over two handlers) and the pipeline (substitute, consumer, part).
/// </summary>
internal static class HandlerChain
{
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

    /// <summary>Wraps <paramref name="first"/> then <paramref name="second"/> into one callback value (the public merge's chain, RND-EVT-02).</summary>
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
