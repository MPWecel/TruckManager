using TruckManager.Domain.Aggregates.Trucks;
using TruckManager.Application.Trucks.DTOs;

namespace TruckManager.Application.Trucks.Queries;

internal static class TruckQueryExtensions
{
    internal static IQueryable<TruckDto> SelectTruckDto(this IQueryable<Truck> source) => source.Select(TruckDto.Projection);

    internal static IQueryable<TruckSummaryDto> SelectTruckSummary(this IQueryable<Truck> source) => source.Select(TruckSummaryDto.Projection);

}
