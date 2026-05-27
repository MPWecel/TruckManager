using Microsoft.EntityFrameworkCore;

using TruckManager.Common.Results;
using TruckManager.Domain.ValueObjects;
using TruckManager.Application.Abstractions.Cqrs;
using TruckManager.Application.Abstractions.Persistence;
using TruckManager.Application.Trucks.DTOs;

namespace TruckManager.Application.Trucks.Queries.GetTruckById;

// V1 reads directly from IApplicationDbContext per Phase 5 decision #5 + ADR-0040.
// V2 will introduce a read-only IDbContext / projection abstraction for the read side and the DbContext dependency below should move there.
public sealed class GetTruckByIdHandler : IQueryHandler<GetTruckByIdQuery, Result<TruckDto>>
{
    private readonly IApplicationDbContext _ctx;

    public GetTruckByIdHandler(IApplicationDbContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        _ctx = ctx;
    }

    public async Task<Result<TruckDto>> HandleAsync(GetTruckByIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        TruckDto? dto = await _ctx.Trucks.Where(t => t.Id == new TruckId(query.TruckId))
                                         .SelectTruckDto()
                                         .FirstOrDefaultAsync(cancellationToken);

        Result<TruckDto> result = dto is not null ? 
                                      Result<TruckDto>.Success(dto) : 
                                      Result<TruckDto>.Failure(NotFound(query.TruckId));
        return result;
    }

    private static Error NotFound(Guid id) => new("truck.not_found", $"Truck {id} was not found.", EErrorType.NotFound);

}
