using System.Reflection;
using System.Text.Json;

using TruckManager.Domain.Events;

namespace TruckManager.Infrastructure.Persistence.Serialization;

// [ADR-0030]   Concrete codec. Scans the Domain assembly at construction for every
// concrete record deriving from DomainEvent and indexes them by Type.Name (the
// EventType discriminator stored in TruckDomainEvents.EventType). The JsonSerializerOptions
// instance is reused for every call — STJ caches the metadata behind the scenes.
//
// Registered as a singleton (no per-request state). Section F wires the DI.
public sealed class DomainEventSerializer : IDomainEventSerializer
{
    private readonly IReadOnlyDictionary<string, Type> _eventTypesByName;
    private readonly JsonSerializerOptions _options;

    public DomainEventSerializer()
    {
        Assembly domainAssembly = typeof(DomainEvent).Assembly;

        _eventTypesByName = domainAssembly.GetTypes()
                                          .Where(t => !t.IsAbstract && typeof(DomainEvent).IsAssignableFrom(t))
                                          .ToDictionary(t => t.Name, StringComparer.Ordinal);

        _options = new JsonSerializerOptions
        {
            Converters =
            {
                new StronglyTypedIdJsonConverterFactory(),
                new TruckCodeJsonConverter(),
                new TruckNameJsonConverter(),
                new TruckDescriptionJsonConverter(),
            },
        };
    }

    public (string EventType, string PayloadJson) Serialize(DomainEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt);

        Type   concrete = evt.GetType();
        string payload  = JsonSerializer.Serialize(evt, concrete, _options);
        return (concrete.Name, payload);
    }

    public DomainEvent Deserialize(string eventType, string payloadJson)
    {
        ArgumentNullException.ThrowIfNull(eventType);
        ArgumentNullException.ThrowIfNull(payloadJson);

        if (!_eventTypesByName.TryGetValue(eventType, out Type? concrete))
            throw new InvalidOperationException(
                $"Unknown domain event type '{eventType}'. Type discovery scans the Domain assembly at startup; ensure the concrete event derives from DomainEvent and the assembly is loaded."
            );

        DomainEvent? result = (DomainEvent?)JsonSerializer.Deserialize(payloadJson, concrete, _options);
        return result ?? throw new InvalidOperationException(
            $"Deserialisation of '{eventType}' returned null. Payload: {payloadJson}"
        );
    }
}
