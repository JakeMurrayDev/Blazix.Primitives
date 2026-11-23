namespace Blazix.Primitives.Fixtures.Client.AccordionFixtures;

/// <summary>
/// The record of one case's callbacks, which <see cref="AccordionCaseFrame"/> renders as data attributes: the item
/// <c>OnOpenChange</c> calls and the root <c>OnValueChange</c> calls, with the arguments of the last of each. A case can
/// cancel the requests of one reason in either callback, and a controlled case applies the requested value unless told to
/// ignore it.
/// </summary>
public sealed class AccordionCase
{
    public int ItemCalls { get; private set; }

    public string LastItemOpen { get; private set; } = "";

    public string LastItemReason { get; private set; } = "";

    public string LastItemEvent { get; private set; } = "";

    public int ValueCalls { get; private set; }

    public string LastValue { get; private set; } = "";

    public string LastValueReason { get; private set; } = "";

    public string LastValueEvent { get; private set; } = "";

    /// <summary>The value of a controlled case.</summary>
    public IReadOnlyList<string> Value { get; set; } = [];

    /// <summary>Cancels the item requests with this reason.</summary>
    public EventReason? CancelItem { get; set; }

    /// <summary>Cancels the root requests with this reason.</summary>
    public EventReason? CancelValue { get; set; }

    /// <summary>Leaves a controlled case's value as it is.</summary>
    public bool IgnoreRequests { get; set; }

    public void OnOpenChange(ValueChangeEventArgs<bool> args)
    {
        ItemCalls++;
        LastItemOpen = args.Value ? "true" : "false";
        LastItemReason = args.Details.Reason.ToLiteral();
        EventSnapshot e = args.Details.Event;
        LastItemEvent = $"{e.Kind}:{e.Type}:detail={e.Detail}";
        if (CancelItem == args.Details.Reason)
        {
            args.Details.Cancel();
        }
    }

    public void OnValueChange(ValueChangeEventArgs<IReadOnlyList<string>> args)
    {
        ValueCalls++;
        LastValue = string.Join(',', args.Value);
        LastValueReason = args.Details.Reason.ToLiteral();
        EventSnapshot e = args.Details.Event;
        LastValueEvent = $"{e.Kind}:{e.Type}:detail={e.Detail}";
        if (CancelValue == args.Details.Reason)
        {
            args.Details.Cancel();
            return;
        }

        if (!IgnoreRequests)
        {
            Value = args.Value;
        }
    }
}
