using TruckManager.Application.Abstractions.Cqrs;

namespace TruckManager.Application.Trucks.Commands.DeleteTruck;

// [RequiresPermission("truck.delete")]
public sealed record DeleteTruckCommand(Guid TruckId) : ICommand;
