using System.Text.Json;
using System.Text.Json.Serialization;

namespace Blazix.Primitives.Interop;

// DRW-API-02; M6 track 8b slice 3 (2026-11-20): the module receives each snap point as the consumer wrote it, a JSON
// number or string, and resolves it itself (js/drawer.ts, DRW-STA-05).
/// <summary>Writes a snap point as a JSON number or string, and <see langword="null"/> for none.</summary>
internal sealed class SnapPointJsonConverter : JsonConverter<SnapPointValue?>
{
    /// <inheritdoc />
    public override bool HandleNull => true;

    /// <inheritdoc />
    public override SnapPointValue? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => ReadValue(ref reader);

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, SnapPointValue? value, JsonSerializerOptions options) => WriteValue(writer, value);

    internal static SnapPointValue? ReadValue(ref Utf8JsonReader reader) => reader.TokenType switch
    {
        JsonTokenType.Number => new SnapPointValue(reader.GetDouble()),
        JsonTokenType.String => new SnapPointValue(reader.GetString()!),
        _ => (SnapPointValue?)null,
    };

    internal static void WriteValue(Utf8JsonWriter writer, SnapPointValue? value)
    {
        if (value is { } snapPoint && snapPoint.TryGetValue(out double number))
        {
            writer.WriteNumberValue(number);
        }
        else if (value is { } textPoint && textPoint.TryGetValue(out string? text))
        {
            writer.WriteStringValue(text);
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}

/// <summary>Writes a list of snap points as a JSON array of numbers and strings.</summary>
internal sealed class SnapPointListJsonConverter : JsonConverter<IReadOnlyList<SnapPointValue>?>
{
    /// <inheritdoc />
    public override bool HandleNull => true;

    /// <inheritdoc />
    public override IReadOnlyList<SnapPointValue>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            return null;
        }

        List<SnapPointValue> values = [];
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (SnapPointJsonConverter.ReadValue(ref reader) is { } value)
            {
                values.Add(value);
            }
        }

        return values;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, IReadOnlyList<SnapPointValue>? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartArray();
        foreach (SnapPointValue snapPoint in value)
        {
            SnapPointJsonConverter.WriteValue(writer, snapPoint);
        }

        writer.WriteEndArray();
    }
}
