namespace TruckManager.Application.Trucks.DTOs;

public sealed record PagedListDto<T>(
                                        IReadOnlyList<T> Items,
                                        int Page,
                                        int PageSize,
                                        long TotalCount
                                    );
