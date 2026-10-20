using Blazix.Primitives.Fixtures.Client.Harness;
using Blazix.Primitives.Interop;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Fixtures.Client.Probe;

/// <summary>
/// The harness canary's twin (issue 33, deliverable 6): the smallest twin that exercises every edge of the contract
/// of ADR 04 §3 against the real JavaScript runtime in both render modes.
/// </summary>
internal sealed class ProbeTwin(IJSRuntime jsRuntime) : TwinBase<ProbeTwin, ProbeOptions>(jsRuntime, ModulePath, FixtureJsonContext.Default.ProbeOptions)
{
    // The client project is served at the site root by the host (its wwwroot is not under _content/).
    public const string ModulePath = "./js/probe.js";

    /// <summary>Raised once per click the module reported (one message per state change).</summary>
    public event Action<ProbeClick>? Clicked;

    /// <summary>The C# chain of the module's one decision point, <c>activate</c>.</summary>
    public Func<string, JsEventInfo, DecisionPolicy>? Decider { get; set; }

    [JSInvokable]
    public void OnClick(string payloadJson)
    {
        Clicked?.Invoke(Deserialize(payloadJson, FixtureJsonContext.Default.ProbeClick));
    }

    protected override DecisionPolicy OnDecide(string point, JsEventInfo eventInfo)
    {
        return Decider?.Invoke(point, eventInfo) ?? DecisionPolicy.Run;
    }
}
