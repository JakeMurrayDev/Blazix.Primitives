namespace Blazix.Primitives.Fixtures.Client.CollapsibleFixtures;

/// <summary>
/// The record of one case's <c>OnOpenChange</c> calls, which <see cref="CollapsibleCaseFrame"/> renders as data
/// attributes: how many, and the arguments of the last. A case can cancel the requests of one reason, and a controlled
/// case applies the requested state unless it is told to ignore it.
/// </summary>
public sealed class CollapsibleCase
{
    public int Calls { get; private set; }

    public string LastOpen { get; private set; } = "";

    public string LastReason { get; private set; } = "";

    public string LastEvent { get; private set; } = "";

    public string LastTrigger { get; private set; } = "";

    /// <summary>The open state of a controlled case.</summary>
    public bool Open { get; set; }

    /// <summary>Cancels the requests with this reason.</summary>
    public EventReason? CancelReason { get; set; }

    /// <summary>Leaves a controlled case's open state as it is.</summary>
    public bool IgnoreRequests { get; set; }

    /// <summary>Leaves a controlled case's open state as it is for the requests with this reason.</summary>
    public EventReason? IgnoreReason { get; set; }

    public void OnOpenChange(ValueChangeEventArgs<bool> args)
    {
        Calls++;
        LastOpen = args.Value ? "true" : "false";
        LastReason = args.Details.Reason.ToLiteral();
        EventSnapshot e = args.Details.Event;
        LastEvent = $"{e.Kind}:{e.Type}:detail={e.Detail}";
        LastTrigger = args.Details.Trigger is null ? "none" : "set";
        if (CancelReason == args.Details.Reason)
        {
            args.Details.Cancel();
            return;
        }

        if (!IgnoreRequests && IgnoreReason != args.Details.Reason)
        {
            Open = args.Value;
        }
    }
}
