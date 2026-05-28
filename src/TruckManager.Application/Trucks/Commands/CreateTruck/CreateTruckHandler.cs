using TruckManager.Common.Abstractions;
using TruckManager.Common.Results;
using TruckManager.Domain.Aggregates.Trucks;
using TruckManager.Domain.ValueObjects;
using TruckManager.Application.Abstractions;
using TruckManager.Application.Abstractions.Cqrs;
using TruckManager.Application.Abstractions.Persistence;

namespace TruckManager.Application.Trucks.Commands.CreateTruck;

public sealed class CreateTruckHandler : ICommandHandler<CreateTruckCommand, Result<TruckId>>
{
    private readonly IApplicationDbContext _ctx;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _clock;

    public CreateTruckHandler(IApplicationDbContext ctx, ICurrentUserService currentUser, IDateTimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(clock);

        _ctx         = ctx;
        _currentUser = currentUser;
        _clock       = clock;
    }

    public Task<Result<TruckId>> HandleAsync(CreateTruckCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        Result<TruckCode> codeResult = TruckCode.Create(command.Code);
        if (!codeResult.IsSuccess) 
            return Task.FromResult(Result<TruckId>.Failure(codeResult.Errors));

        Result<TruckName> nameResult = TruckName.Create(command.Name);
        if (!nameResult.IsSuccess) 
            return Task.FromResult(Result<TruckId>.Failure(nameResult.Errors));

        Result<TruckDescription> descResult = TruckDescription.Create(command.Description);
        if (!descResult.IsSuccess) 
            return Task.FromResult(Result<TruckId>.Failure(descResult.Errors));

        Guid userId = _currentUser.UserId ?? Guid.Empty;
        TenantId tenantId = new(command.TenantId);
        TruckId truckId = new(Guid.CreateVersion7(_clock.UtcNow));

        Result<Truck> truckResult = Truck.Create(
                                                    id: truckId,
                                                    tenantId: tenantId,
                                                    code: codeResult.Value!,
                                                    name: nameResult.Value!,
                                                    description: descResult.Value!,
                                                    initialStatus: command.InitialStatus,
                                                    clock: _clock,
                                                    createdByUserId: userId
                                                );

        if (!truckResult.IsSuccess) 
            return Task.FromResult(Result<TruckId>.Failure(truckResult.Errors));

        _ctx.Trucks.Add(truckResult.Value!);
        return Task.FromResult(Result<TruckId>.Success(truckId));
    }
}
