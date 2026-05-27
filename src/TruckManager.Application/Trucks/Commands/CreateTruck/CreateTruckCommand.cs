using TruckManager.Common.Results;
using TruckManager.Domain.Enums;
using TruckManager.Domain.ValueObjects;
using TruckManager.Application.Abstractions.Cqrs;

namespace TruckManager.Application.Trucks.Commands.CreateTruck;

// [RequiresPermission("truck.write")]
public sealed record CreateTruckCommand(
                                           Guid TenantId,
                                           string Code,
                                           string Name,
                                           string? Description,
                                           ETruckStatus InitialStatus
                                       ) : ICommand<Result<TruckId>>;
