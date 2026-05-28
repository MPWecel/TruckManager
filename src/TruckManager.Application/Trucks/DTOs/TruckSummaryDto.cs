using System.Linq.Expressions;

using TruckManager.Domain.Aggregates.Trucks;

namespace TruckManager.Application.Trucks.DTOs;

public sealed record TruckSummaryDto(
                                        Guid Id,
                                        Guid TenantId,
                                        string Code,
                                        string Name,
                                        int Status,
                                        DateTimeOffset UpdatedAtUtc
                                    )
{
    // EF-translatable projection — use via IQueryable<Truck>.Select(TruckSummaryDto.Projection).
    // Only the columns needed for list views are selected. See architecture.md §19.4 + ADR-0040.
    public static readonly Expression<Func<Truck, TruckSummaryDto>> Projection = 
        truck => new TruckSummaryDto(
                                        truck.Id.Value,
                                        truck.TenantId.Value,
                                        truck.Code.Value,
                                        truck.Name.Value,
                                        (int)(truck.Status),
                                        truck.UpdatedAtUtc
                                    );
}
