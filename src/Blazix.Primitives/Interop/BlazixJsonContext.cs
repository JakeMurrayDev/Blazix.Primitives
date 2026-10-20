using System.Text.Json.Serialization;

namespace Blazix.Primitives.Interop;

/// <summary>
/// The one source-generated JSON context of the library (ADR 16 rule 4): every twin options record and every
/// callback payload is listed here, so no interop payload needs reflection. Options travel as a JSON string the
/// twin serialises itself; callback payloads arrive as JSON strings the twin deserialises itself.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(JsEventInfo))]
internal sealed partial class BlazixJsonContext : JsonSerializerContext;
