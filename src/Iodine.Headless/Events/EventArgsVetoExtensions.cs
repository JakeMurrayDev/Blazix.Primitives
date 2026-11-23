using System.Runtime.CompilerServices;

namespace Blazix.Primitives;

// RND-EVT-03/04, ADR 02 decision 3, named by ADR 08 §4; RND-EVT-05; ADR 04
/// <summary>
/// Provides the handler veto: a consumer handler calls <see cref="PreventPartHandler"/> on the event to stop the handlers
/// that would run after it in the chain, the part's own handler above all.
/// </summary>
/// <remarks>
/// The flag lives on the event instance, which Blazor creates afresh per dispatch, so its scope is one dispatch. The veto
/// neither prevents the browser default nor stops propagation; those are decided in JavaScript.
/// </remarks>
public static class EventArgsVetoExtensions
{
    private static readonly ConditionalWeakTable<EventArgs, object> s_vetoed = new();
    private static readonly object s_marker = new();

    /// <summary>
    /// Stops the handlers after the current one in this dispatch's chain.
    /// </summary>
    /// <param name="e">The event being dispatched.</param>
    /// <exception cref="ArgumentNullException"><paramref name="e"/> is <see langword="null"/>.</exception>
    public static void PreventPartHandler(this EventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        s_vetoed.AddOrUpdate(e, s_marker);
    }

    /// <summary>
    /// Determines whether <see cref="PreventPartHandler"/> was called on an event.
    /// </summary>
    /// <param name="e">The event being dispatched.</param>
    /// <returns><see langword="true"/> if <see cref="PreventPartHandler"/> was called on <paramref name="e"/>; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="e"/> is <see langword="null"/>.</exception>
    public static bool PartHandlerPrevented(this EventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        return s_vetoed.TryGetValue(e, out _);
    }

    /// <summary>
    /// Clears the veto flag on an event.
    /// </summary>
    /// <param name="e">The event whose flag to clear.</param>
    /// <remarks>Used after a chain ran on the shared <see cref="EventArgs.Empty"/> instance, which outlives the dispatch.</remarks>
    internal static void ClearPartHandlerVeto(this EventArgs e) => s_vetoed.Remove(e);
}
