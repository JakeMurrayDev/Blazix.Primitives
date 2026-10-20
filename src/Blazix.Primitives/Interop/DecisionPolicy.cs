namespace Blazix.Primitives.Interop;

// ADR 04 §3 (decision points and fallback policies), ADR 01 relaxation table.
/// <summary>
/// The verdict of a decision point: what the JavaScript module does with the browser dispatch it owns. On
/// WebAssembly the C# handler chain returns one of these values synchronously inside the dispatch; on Server the
/// module applies its fallback policy without calling C#.
/// </summary>
internal enum DecisionPolicy
{
    /// <summary>Run the module's own handling. The fallback for composite and button handling.</summary>
    Run,

    /// <summary>Skip the module's own handling; a consumer vetoed it. The fallback for chip removal.</summary>
    Stop,

    /// <summary>Begin the loop, such as a drag. The fallback for drag starts.</summary>
    Start,

    /// <summary>Call <c>preventDefault()</c> and <c>stopPropagation()</c> on the event. The fallback for Escape on the innermost open popup.</summary>
    PreventAndStop,
}

/// <summary>Conversions between <see cref="DecisionPolicy"/> and the literals the JavaScript <c>decide()</c> helper understands.</summary>
internal static class DecisionPolicyLiterals
{
    /// <summary>Returns the kebab-case literal of a policy.</summary>
    /// <param name="policy">The policy to convert.</param>
    /// <returns><c>run</c>, <c>stop</c>, <c>start</c> or <c>prevent-and-stop</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="policy"/> is not a defined <see cref="DecisionPolicy"/> value.</exception>
    public static string ToLiteral(this DecisionPolicy policy)
    {
        return policy switch
        {
            DecisionPolicy.Run => "run",
            DecisionPolicy.Stop => "stop",
            DecisionPolicy.Start => "start",
            DecisionPolicy.PreventAndStop => "prevent-and-stop",
            _ => throw new ArgumentOutOfRangeException(nameof(policy), policy, null),
        };
    }
}
