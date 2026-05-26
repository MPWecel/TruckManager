using System.Text.Json;
using System.Text.Json.Serialization;

using TruckManager.Domain.ValueObjects;

namespace TruckManager.Infrastructure.Persistence.Serialization;

// [ADR-0030]   Reads a JSON string as a TruckCode via FromTrusted (skip validator — the
// payload was produced by Serialize, so the value is guaranteed already-normalised).
// Writes the raw normalised string.
public sealed class TruckCodeJsonConverter : JsonConverter<TruckCode>
{
    public override TruckCode? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        string? raw = reader.GetString();
        return raw is null ? null : TruckCode.FromTrusted(raw);
    }

    public override void Write(Utf8JsonWriter writer, TruckCode value, JsonSerializerOptions options)
    {
        if (value is null)
            writer.WriteNullValue();
        else
            writer.WriteStringValue(value.Value);
    }
}
