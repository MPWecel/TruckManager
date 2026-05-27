using TruckManager.Domain.Enums;
using TruckManager.Application.Abstractions.Cqrs;

namespace TruckManager.Application.Trucks.Commands.ChangeTruckStatus;

// [RequiresPermission("truck.status.change")]
public sealed record ChangeTruckStatusCommand(
                                                 Guid TruckId,
                                                 ETruckStatus NewStatus
                                             ) : ICommand;
