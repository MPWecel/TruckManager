using TruckManager.Domain.Events;

namespace TruckManager.Infrastructure.Persistence.Serialization;

// [ADR-0030]   Contract for the polymorphic domain-event payload codec used by the
// DomainEventPersistenceInterceptor. Serialises the full record (base + payload fields)
// into PayloadJson; EventType is the .NET type name (Truck`Created`, `TruckRenamed`,
// etc.) and serves as the discriminator on deserialisation.
public interface IDomainEventSerializer
{
    (string EventType, string PayloadJson) Serialize(DomainEvent evt);
    DomainEvent Deserialize(string eventType, string payloadJson);
}
