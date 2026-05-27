using Microsoft.EntityFrameworkCore;

using TruckManager.Common.Results;
using TruckManager.Domain.Aggregates.Trucks;
using TruckManager.Application.Abstractions.Cqrs;
using TruckManager.Application.Abstractions.Persistence;
using TruckManager.Application.Trucks.DTOs;

namespace TruckManager.Application.Trucks.Queries.ListTrucks;

// V1 reads directly from IApplicationDbContext per Phase 5 decision #5 + ADR-0040.
// V2 will introduce a read-only IDbContext / projection abstraction for the read side and the DbContext dependency below should move there.
public sealed class ListTrucksHandler : IQueryHandler<ListTrucksQuery, Result<PagedListDto<TruckSummaryDto>>>
{
    private readonly IApplicationDbContext _ctx;

    public ListTrucksHandler(IApplicationDbContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        _ctx = ctx;
    }

    public async Task<Result<PagedListDto<TruckSummaryDto>>> HandleAsync(ListTrucksQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        IQueryable<Truck> baseQuery = _ctx.Trucks;

        if (query.StatusFilter.HasValue)
            baseQuery = baseQuery.Where(t => t.Status == query.StatusFilter.Value);

        long totalCount = await baseQuery.LongCountAsync(cancellationToken);

        List<TruckSummaryDto> items = await baseQuery.OrderBy(t => t.CreatedAtUtc)
                                                     .Skip((query.Page - 1) * query.PageSize)
                                                     .Take(query.PageSize)
                                                     .SelectTruckSummary()
                                                     .ToListAsync(cancellationToken);

        PagedListDto<TruckSummaryDto> listDto = new(items, query.Page, query.PageSize, totalCount);
        Result<PagedListDto<TruckSummaryDto>> result = Result<PagedListDto<TruckSummaryDto>>.Success(listDto);
        return result;
    }
}
