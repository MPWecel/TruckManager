using System.Linq.Expressions;

using TruckManager.Domain.Aggregates.Trucks;

namespace TruckManager.Application.Trucks.DTOs;

public sealed record TruckDto(
                                 Guid Id,
                                 Guid TenantId,
                                 string Code,
                                 string Name,
                                 string? Description,
                                 int Status,
                                 ulong Version,
                                 DateTimeOffset CreatedAtUtc,
                                 Guid CreatedByUserId,
                                 DateTimeOffset UpdatedAtUtc,
                                 Guid UpdatedByUserId,
                                 DateTimeOffset? DeletedAtUtc,
                                 Guid? DeletedByUserId,
                                 bool IsDeleted
                             )
{
    // EF-translatable projection — use via IQueryable<Truck>.Select(TruckDto.Projection).
    // VO property accesses (.Value, .IsEmpty) are resolved by EF Core's result materializer on the client after the SQL query; the column set, filtering, and paging all stay server-side. See architecture.md §19.4 + ADR-0040.
    public static readonly Expression<Func<Truck, TruckDto>> Projection = 
        truck => new TruckDto(
                                 truck.Id.Value,
                                 truck.TenantId.Value,
                                 truck.Code.Value,
                                 truck.Name.Value,
                                 truck.Description.IsEmpty ? null : truck.Description.Value,
                                 (int)(truck.Status),
                                 truck.ConcurrencyStamp.Version,
                                 truck.CreatedAtUtc,
                                 truck.CreatedByUserId,
                                 truck.UpdatedAtUtc,
                                 truck.UpdatedByUserId,
                                 truck.DeletedAtUtc,
                                 truck.DeletedByUserId,
                                 truck.IsDeleted
                             );
}
