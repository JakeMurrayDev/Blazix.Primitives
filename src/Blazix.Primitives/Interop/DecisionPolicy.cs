namespace Blazix.Primitives.Interop;

/// <summary>
/// The verdict of a decision point (ADR 04 §3): what the JavaScript module does with the dispatch it owns. On
/// WebAssembly the C# chain returns one of these synchronously; elsewhere the module applies its fallback policy.
/// </summary>
internal enum DecisionPolicy
{
    /// <summary>Run the module's handling (the fallback for composite and button handling).</summary>
    Run,

    /// <summary>Skip the module's handling (a consumer veto; the fallback for ChipRemove).</summary>
    Stop,

    /// <summary>Begin the loop (the fallback for drags).</summary>
    Start,

    /// <summary>Call <c>preventDefault()</c> and <c>stopPropagation()</c> (the fallback for Escape on the innermost popup).</summary>
    PreventAndStop,
}

internal static class DecisionPolicyLiterals
{
    /// <summary>The kebab-case literal the JavaScript <c>decide()</c> helper understands (<c>js/decide.ts</c>).</summary>
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
