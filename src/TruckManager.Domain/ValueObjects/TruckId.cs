using TruckManager.Common.Abstractions;

namespace TruckManager.Domain.ValueObjects;

// [ADR-0023]   Strongly-typed Truck identity
// Mirrors TenantId; intentionally no implicit conversion to Guid — force .Value property call at call sites to keep type safety crisp.
public sealed record TruckId(Guid Value) : IStronglyTypedId<Guid>;
