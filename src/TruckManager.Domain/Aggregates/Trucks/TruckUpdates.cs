using TruckManager.Domain.ValueObjects;

namespace TruckManager.Domain.Aggregates.Trucks;

// DTO for Truck.Update. Null value passed on nullable fields mean "don't change". To clear the description, pass TruckDescription.Empty explicitly (not null).
// Future updatable fields are added here, keeping Truck.Update's signature stable.
public sealed record TruckUpdates(TruckName? Name = null, TruckDescription? Description = null);
