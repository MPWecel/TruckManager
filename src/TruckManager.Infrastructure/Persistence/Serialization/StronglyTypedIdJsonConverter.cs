using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

using TruckManager.Common.Abstractions;

namespace TruckManager.Infrastructure.Persistence.Serialization;

// [ADR-0023 / ADR-0030]   Mirror of StronglyTypedIdValueConverter (EF Core) for the
// System.Text.Json side: any closed type implementing IStronglyTypedId<TValue> is
// serialised as its bare TValue (e.g., TenantId → uuid string, TruckId → uuid string)
// and rehydrated through the matching (TValue) constructor. One-time reflection per
// closed type to compile the constructor delegate; zero per-event cost thereafter.
public sealed class StronglyTypedIdJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
        => GetMarkerInterface(typeToConvert) is not null;

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        Type marker    = GetMarkerInterface(typeToConvert)
                         ?? throw new InvalidOperationException($"{typeToConvert.FullName} does not implement IStronglyTypedId<>.");
        Type valueType = marker.GetGenericArguments()[0];
        Type closed    = typeof(StronglyTypedIdJsonConverter<,>).MakeGenericType(typeToConvert, valueType);
        return (JsonConverter?)Activator.CreateInstance(closed);
    }

    private static Type? GetMarkerInterface(Type type) =>
        type.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IStronglyTypedId<>));
}

public sealed class StronglyTypedIdJsonConverter<TId, TValue> : JsonConverter<TId>
    where TId : class, IStronglyTypedId<TValue>
    where TValue : struct
{
    private static readonly Func<TValue, TId> Factory = BuildFactory();

    public override TId? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        TValue value = JsonSerializer.Deserialize<TValue>(ref reader, options);
        return Factory(value);
    }

    public override void Write(Utf8JsonWriter writer, TId value, JsonSerializerOptions options)
    {
        if (value is null)
            writer.WriteNullValue();
        else
            JsonSerializer.Serialize(writer, value.Value, options);
    }

    private static Func<TValue, TId> BuildFactory()
    {
        ConstructorInfo? ctor = typeof(TId).GetConstructor(
                                                              bindingAttr: BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                                                              binder: null,
                                                              types: [typeof(TValue)],
                                                              modifiers: null
                                                          );

        if (ctor is null)
            throw new InvalidOperationException(
                $"Strongly-typed ID '{typeof(TId).FullName}' must declare a single-parameter constructor taking {typeof(TValue).Name} so the JSON converter can rehydrate it."
            );

        ParameterExpression param  = Expression.Parameter(typeof(TValue), "value");
        NewExpression        newEx = Expression.New(ctor, param);
        return Expression.Lambda<Func<TValue, TId>>(newEx, param).Compile();
    }
}
