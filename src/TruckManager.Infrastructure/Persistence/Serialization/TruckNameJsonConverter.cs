using System.Text.Json;
using System.Text.Json.Serialization;

using TruckManager.Domain.ValueObjects;

namespace TruckManager.Infrastructure.Persistence.Serialization;

// [ADR-0030]   Same pattern as TruckCodeJsonConverter.
public sealed class TruckNameJsonConverter : JsonConverter<TruckName>
{
    public override TruckName? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        string? raw = reader.GetString();
        return raw is null ? null : TruckName.FromTrusted(raw);
    }

    public override void Write(Utf8JsonWriter writer, TruckName value, JsonSerializerOptions options)
    {
        if (value is null)
            writer.WriteNullValue();
        else
            writer.WriteStringValue(value.Value);
    }
}
