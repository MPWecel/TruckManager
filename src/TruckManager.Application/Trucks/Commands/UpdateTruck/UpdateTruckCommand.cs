using TruckManager.Application.Abstractions.Cqrs;

namespace TruckManager.Application.Trucks.Commands.UpdateTruck;

// Null means "don't change that field". Both being null is a valid no-op (returns Success, no event, no stamp bump).
// [RequiresPermission("truck.write")]
public sealed record UpdateTruckCommand(
                                           Guid TruckId,
                                           string? Name,
                                           string? Description
                                       ) : ICommand;
