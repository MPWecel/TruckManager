using Microsoft.EntityFrameworkCore;

using TruckManager.Common.Abstractions;
using TruckManager.Common.Results;
using TruckManager.Domain.Aggregates.Trucks;
using TruckManager.Domain.ValueObjects;
using TruckManager.Application.Abstractions;
using TruckManager.Application.Abstractions.Cqrs;
using TruckManager.Application.Abstractions.Persistence;

namespace TruckManager.Application.Trucks.Commands.UpdateTruck;

public sealed class UpdateTruckHandler : ICommandHandler<UpdateTruckCommand>
{
    private readonly IApplicationDbContext _ctx;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _clock;

    public UpdateTruckHandler(IApplicationDbContext ctx, ICurrentUserService currentUser, IDateTimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(clock);

        _ctx         = ctx;
        _currentUser = currentUser;
        _clock       = clock;
    }

    public async Task<Result> HandleAsync(UpdateTruckCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        Truck? truck = await _ctx.Trucks.IgnoreQueryFilters()
                                        .FirstOrDefaultAsync(t => t.Id == new TruckId(command.TruckId), cancellationToken);

        if (truck is null)
            return Result.Failure(NotFound(command.TruckId));

        TruckName? newName = null;
        if (command.Name is not null)
        {
            Result<TruckName> r = TruckName.Create(command.Name);
            if (!r.IsSuccess) 
                return Result.Failure(r.Errors);
            
            newName = r.Value;
        }

        TruckDescription? newDesc = null;
        if (command.Description is not null)
        {
            Result<TruckDescription> r = TruckDescription.Create(command.Description);
            if (!r.IsSuccess) 
                return Result.Failure(r.Errors);
            
            newDesc = r.Value;
        }

        Guid userId = _currentUser.UserId ?? Guid.Empty;
        return truck.Update(new TruckUpdates(newName, newDesc), _clock, userId);
    }

    private static Error NotFound(Guid id) => new("truck.not_found", $"Truck {id} was not found.", EErrorType.NotFound);

}
