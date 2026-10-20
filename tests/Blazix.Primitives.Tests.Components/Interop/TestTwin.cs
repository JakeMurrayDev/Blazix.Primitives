using System.Text.Json.Serialization;
using Blazix.Primitives.Interop;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components.Interop;

/// <summary>The options record of the test twin: the shared style string plus one field of its own.</summary>
internal sealed record TestTwinOptions : TwinOptions
{
    public int Threshold { get; init; }
}

/// <summary>The payload of the test twin's one callback.</summary>
internal sealed record TestTwinPayload(string Name, int Count);

/// <summary>Every options record and callback payload of the test twin (ADR 16 rule 4, mirrored for the tests).</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(TestTwinOptions))]
[JsonSerializable(typeof(TestTwinPayload))]
internal sealed partial class TestTwinJsonContext : JsonSerializerContext;

/// <summary>
/// A twin with no module behind it: enough surface to drive the contract of <see cref="TwinBase{TTwin, TOptions}"/>
/// from bUnit's JS interop mock.
/// </summary>
internal sealed class TestTwin(IJSRuntime jsRuntime) : TwinBase<TestTwin, TestTwinOptions>(jsRuntime, ModulePath, TestTwinJsonContext.Default.TestTwinOptions)
{
    public const string ModulePath = "./_content/Blazix.Primitives/js/test-twin.js";

    public List<(string Point, JsEventInfo Event)> Decisions { get; } = [];

    public List<TestTwinPayload> Reported { get; } = [];

    public DecisionPolicy NextVerdict { get; set; } = DecisionPolicy.Run;

    /// <summary>The module's one callback: JavaScript sends the payload as JSON, the twin exposes it typed.</summary>
    [JSInvokable]
    public void Report(string payloadJson)
    {
        Reported.Add(Deserialize(payloadJson, TestTwinJsonContext.Default.TestTwinPayload));
    }

    protected override DecisionPolicy OnDecide(string point, JsEventInfo eventInfo)
    {
        Decisions.Add((point, eventInfo));
        return NextVerdict;
    }
}
