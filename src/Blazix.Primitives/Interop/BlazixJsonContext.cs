using System.Text.Json.Serialization;

namespace Blazix.Primitives.Interop;

/// <summary>
/// The library's source-generated JSON context for JavaScript interop. Every twin options record, every callback
/// payload and the event snapshot are listed here, so no interop payload is serialized through reflection. Options
/// travel to JavaScript as a JSON string the twin produces; callback payloads arrive as JSON strings the twin parses.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(EventSnapshot))]
[JsonSerializable(typeof(ButtonOptions))]
[JsonSerializable(typeof(ButtonInteraction))]
[JsonSerializable(typeof(ButtonHoldEvent))]
[JsonSerializable(typeof(NativeButtonMismatch))]
[JsonSerializable(typeof(DomOptions))]
[JsonSerializable(typeof(ImageStatusChange))]
[JsonSerializable(typeof(TransitionsOptions))]
[JsonSerializable(typeof(TransitionStatusChange))]
[JsonSerializable(typeof(TransitionCompletion))]
[JsonSerializable(typeof(RevealRequest))]
internal sealed partial class BlazixJsonContext : JsonSerializerContext;
