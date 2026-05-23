using TruckManager.Common.Constants;
using TruckManager.Common.Abstractions;

namespace TruckManager.Domain.ValueObjects;

// Strongly-typed tenantID. No implicit conversion to GUID: access via "Value" property geter.
public sealed record TenantId(Guid Value) : IStronglyTypedId<Guid>
{
    public static TenantId Default { get; } = new(Tenants.DefaultTenantId);
}
