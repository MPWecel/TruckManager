using System.Text.Json;
using System.Text.Json.Serialization;

using TruckManager.Domain.ValueObjects;

namespace TruckManager.Infrastructure.Persistence.Serialization;

// [ADR-0030]   Mirrors the EF-side Empty ↔ NULL convention (TruckConfiguration's
// DescriptionConverter). A null or empty JSON string deserialises to TruckDescription.Empty;
// TruckDescription.Empty serialises as JSON null.
public sealed class TruckDescriptionJsonConverter : JsonConverter<TruckDescription>
{
    public override TruckDescription Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return TruckDescription.Empty;

        string? raw = reader.GetString();
        return string.IsNullOrEmpty(raw) ? TruckDescription.Empty : TruckDescription.FromTrusted(raw);
    }

    public override void Write(Utf8JsonWriter writer, TruckDescription value, JsonSerializerOptions options)
    {
        if (value is null || value.IsEmpty)
            writer.WriteNullValue();
        else
            writer.WriteStringValue(value.Value);
    }

    public override bool HandleNull => true;
}
