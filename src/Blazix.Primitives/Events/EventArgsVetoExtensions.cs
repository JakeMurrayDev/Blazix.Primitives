using System.Runtime.CompilerServices;

namespace Blazix.Primitives;

/// <summary>
/// The handler veto (RND-EVT-03/04, ADR 02 decision 3, named by ADR 08 §4): a consumer handler calls
/// <see cref="PreventPartHandler"/> on the event to stop the handlers that would run after it in the chain (the part's
/// own handler above all); <see cref="PartHandlerPrevented"/> reads the flag. The flag lives on the event instance,
/// which Blazor creates afresh per dispatch, so its scope is one dispatch. It neither prevents the browser default nor
/// stops propagation (RND-EVT-05): those are JavaScript policy (ADR 04).
/// </summary>
public static class EventArgsVetoExtensions
{
    private static readonly ConditionalWeakTable<EventArgs, object> s_vetoed = new();
    private static readonly object s_marker = new();

    /// <summary>Stops the handlers after the current one in this dispatch's chain.</summary>
    public static void PreventPartHandler(this EventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        s_vetoed.AddOrUpdate(e, s_marker);
    }

    /// <summary>Whether <see cref="PreventPartHandler"/> was called on this event.</summary>
    public static bool PartHandlerPrevented(this EventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        return s_vetoed.TryGetValue(e, out _);
    }

    /// <summary>Clears the flag; used after a chain ran on the shared <see cref="EventArgs.Empty"/> instance, which outlives the dispatch.</summary>
    internal static void ClearPartHandlerVeto(this EventArgs e) => s_vetoed.Remove(e);
}
