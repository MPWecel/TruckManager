using TruckManager.Common.Results;
using TruckManager.Application.Abstractions.Cqrs;
using TruckManager.Application.Trucks.DTOs;

namespace TruckManager.Application.Trucks.Queries.GetTruckById;

public sealed record GetTruckByIdQuery(Guid TruckId) : IQuery<Result<TruckDto>>;
