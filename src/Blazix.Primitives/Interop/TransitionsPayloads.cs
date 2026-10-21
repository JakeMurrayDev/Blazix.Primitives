using System.Text.Json;
using System.Text.Json.Serialization;

namespace Blazix.Primitives.Interop;

// TRN-API-07, TRN-STA-01 (one message per change of the mirrored status)
/// <summary>The payload of the status report: the transition status and the mounted flag after a change of either.</summary>
/// <param name="Status">The status the element now shows, or <see langword="null"/> for the unset status.</param>
/// <param name="Mounted">The mounted flag.</param>
internal sealed record TransitionStatusChange(
    [property: JsonConverter(typeof(TransitionStatusJsonConverter))] TransitionStatus? Status,
    bool Mounted);

// TRN-ANI-03, TRN-ALG-04/05 (the completion notification)
/// <summary>The payload of the completion report: which side of the cycle finished its animations.</summary>
/// <param name="Open">
/// <see langword="false"/> when the exit finished, after which the element is not mounted and its status is unset;
/// <see langword="true"/> when the enter finished.
/// </param>
internal sealed record TransitionCompletion(bool Open);

// TRN-ANI-32, TRN-EVT-09 (the reveal requests an open with reason none and the beforematch event)
/// <summary>The payload of the reveal report: a <c>beforematch</c> that no C# handler could see inside the dispatch.</summary>
/// <param name="Event">The snapshot of the <c>beforematch</c> event.</param>
internal sealed record RevealRequest(EventSnapshot Event);

/// <summary>Converts a nullable <see cref="TransitionStatus"/> to and from its literal, <c>starting</c>, <c>ending</c>, <c>idle</c> or <see langword="null"/>.</summary>
internal sealed class TransitionStatusJsonConverter : JsonConverter<TransitionStatus?>
{
    /// <inheritdoc />
    public override bool HandleNull => true;

    /// <inheritdoc />
    /// <exception cref="JsonException">The token is not one of the literals.</exception>
    public override TransitionStatus? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("Blazix: a transition status must be a string or `null`.");
        }

        return reader.GetString() switch
        {
            "starting" => TransitionStatus.Starting,
            "ending" => TransitionStatus.Ending,
            "idle" => TransitionStatus.Idle,
            string other => throw new JsonException($"Blazix: `{other}` is not a transition status."),
            null => null,
        };
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, TransitionStatus? value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case TransitionStatus.Starting:
                writer.WriteStringValue("starting");
                break;
            case TransitionStatus.Ending:
                writer.WriteStringValue("ending");
                break;
            case TransitionStatus.Idle:
                writer.WriteStringValue("idle");
                break;
            default:
                writer.WriteNullValue();
                break;
        }
    }
}
