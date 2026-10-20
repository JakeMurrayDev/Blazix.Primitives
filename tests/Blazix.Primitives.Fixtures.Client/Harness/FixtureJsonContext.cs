using System.Text.Json.Serialization;
using Blazix.Primitives.Fixtures.Client.Probe;

namespace Blazix.Primitives.Fixtures.Client.Harness;

/// <summary>The fixtures' own JSON context: the probe twin's records and the diagnostics payload.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ProbeOptions))]
[JsonSerializable(typeof(ProbeClick))]
[JsonSerializable(typeof(Diagnostic[]))]
internal sealed partial class FixtureJsonContext : JsonSerializerContext;
