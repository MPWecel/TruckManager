using Microsoft.EntityFrameworkCore;

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

    public async Task<Result<TruckId>> HandleAsync(CreateTruckCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        Result<TruckCode> codeResult = TruckCode.Create(command.Code);
        if (!codeResult.IsSuccess)
            return Result<TruckId>.Failure(codeResult.Errors);

        Result<TruckName> nameResult = TruckName.Create(command.Name);
        if (!nameResult.IsSuccess)
            return Result<TruckId>.Failure(nameResult.Errors);

        Result<TruckDescription> descResult = TruckDescription.Create(command.Description);
        if (!descResult.IsSuccess)
            return Result<TruckId>.Failure(descResult.Errors);

        TruckCode code = codeResult.Value!;
        TenantId tenantId = new(command.TenantId);

        // Pre-check: is this code already taken by an active truck in this tenant?
        // The partial unique index UX_Trucks_TenantId_Code WHERE IsDeleted = false (ADR-0033) is the source of truth at the DB level; this pre-check converts the common case into a clean Result.Failure(Conflict) (→ 409 via ApiResultExtensions) instead of letting the constraint throw at SaveChanges (which would bubble out as a 500 via GlobalExceptionHandler).
        //
        // The query honours the soft-delete global filter (TruckConfiguration.HasQueryFilter), so deleted trucks don't block code reuse — matches the partial-index semantics by construction.
        //
        // Race: two concurrent creates with the same code can both pass this AnyAsync and then collide on SaveChanges.
        // The DB constraint catches it; the 500 in that narrow window is a known V1 limit — revisit when retry / outbox / proper concurrency translation lands (Phase 7+).
        //
        // Equality form `t.Code == code` works on both providers used by the test+prod stacks:
        //   >  Npgsql translates via the property-level ValueConverter<TruckCode, string> in TruckConfiguration.
        //   >  InMemory evaluates structural equality on the TruckCode record after materialization.
        // EF.Property<string>(t, "Code") was tried first but throws on InMemory ("no coercion operator between TruckCode and String") — same cross-provider class as the Phase 5 OrderBy issue.
        bool codeInUse = await _ctx.Trucks.AnyAsync(
                                                       t => t.TenantId == tenantId && t.Code == code,
                                                       cancellationToken
                                                   );

        if (codeInUse)
            return Result<TruckId>.Failure(CodeAlreadyInUse(code.Value));

        Guid userId = _currentUser.UserId ?? Guid.Empty;
        TruckId truckId = new(Guid.CreateVersion7(_clock.UtcNow));

        Result<Truck> truckResult = Truck.Create(
                                                    id: truckId,
                                                    tenantId: tenantId,
                                                    code: code,
                                                    name: nameResult.Value!,
                                                    description: descResult.Value!,
                                                    initialStatus: command.InitialStatus,
                                                    clock: _clock,
                                                    createdByUserId: userId
                                                );

        if (!truckResult.IsSuccess)
            return Result<TruckId>.Failure(truckResult.Errors);

        _ctx.Trucks.Add(truckResult.Value!);
        return Result<TruckId>.Success(truckId);
    }

    private static Error CodeAlreadyInUse(string code)
        => new(
                  Code:    "truck.code_already_in_use",
                  Message: $"A truck with code '{code}' already exists in this tenant.",
                  Type:    EErrorType.Conflict
              );
}
