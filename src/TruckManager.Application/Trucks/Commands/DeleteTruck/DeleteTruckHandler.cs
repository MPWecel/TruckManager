using Microsoft.EntityFrameworkCore;

using TruckManager.Common.Abstractions;
using TruckManager.Common.Results;
using TruckManager.Domain.Aggregates.Trucks;
using TruckManager.Domain.ValueObjects;
using TruckManager.Application.Abstractions;
using TruckManager.Application.Abstractions.Cqrs;
using TruckManager.Application.Abstractions.Persistence;

namespace TruckManager.Application.Trucks.Commands.DeleteTruck;

public sealed class DeleteTruckHandler : ICommandHandler<DeleteTruckCommand>
{
    private readonly IApplicationDbContext _ctx;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _clock;
    private readonly ICorrelationContext _correlation;

    public DeleteTruckHandler(IApplicationDbContext ctx, ICurrentUserService currentUser, IDateTimeProvider clock, ICorrelationContext correlation)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(correlation);

        _ctx = ctx;
        _currentUser = currentUser;
        _clock = clock;
        _correlation = correlation;
    }

    public async Task<Result> HandleAsync(DeleteTruckCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        Truck? truck = await _ctx.Trucks.IgnoreQueryFilters()
                                        .FirstOrDefaultAsync(t => t.Id == new TruckId(command.TruckId), cancellationToken);

        if (truck is null)
            return Result.Failure(NotFound(command.TruckId));

        Guid userId = _currentUser.UserId ?? Guid.Empty;
        return truck.Delete(_clock, userId, correlationId: _correlation.CorrelationId);
    }

    private static Error NotFound(Guid id) => new("truck.not_found", $"Truck {id} was not found.", EErrorType.NotFound);

}
