using TruckManager.Common.Results;
using TruckManager.Domain.Enums;
using TruckManager.Application.Abstractions.Cqrs;
using TruckManager.Application.Trucks.DTOs;

namespace TruckManager.Application.Trucks.Queries.ListTrucks;

public sealed record ListTrucksQuery(int Page, int PageSize, ETruckStatus? StatusFilter) : IQuery<Result<PagedListDto<TruckSummaryDto>>>;
