using AwesomeAssertions;
using Xunit;

using TruckManager.Common.Results;
using TruckManager.Domain.Enums;
using TruckManager.Infrastructure.Persistence;
using TruckManager.Application.Trucks.DTOs;
using TruckManager.Application.Trucks.Queries.ListTrucks;
using TruckManager.UnitTests.TestHelpers;
using TruckManager.Domain.Aggregates.Trucks;

namespace TruckManager.UnitTests.Application.Tests.Trucks.Queries;

public class ListTrucksHandlerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 5, 13, 13, 37, 0, TimeSpan.Zero);

    // Happy path

    [Fact]
    public async Task HandleAsync_returns_Success_with_empty_page_when_no_trucks()
    {
        // Arrange
        using ApplicationDbContext ctx = TestDbContextFactory.Create();
        ListTrucksHandler handler = new(ctx);
        ListTrucksQuery query = new(Page: 1, PageSize: 10, StatusFilter: null);

        // Act
        Result<PagedListDto<TruckSummaryDto>> result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should()
                        .BeTrue();
        result.Value!.Items.Should()
                           .BeEmpty();
        result.Value.TotalCount.Should()
                               .Be(0);
        result.Value.Page.Should()
                         .Be(1);
        result.Value.PageSize.Should()
                             .Be(10);
    }

    [Fact]
    public async Task HandleAsync_returns_seeded_truck_in_first_page()
    {
        // Arrange
        string dbName = Guid.NewGuid()
                            .ToString();
        FakeDateTimeProvider clock = new(T0);

        using (ApplicationDbContext seedCtx = TestDbContextFactory.Create(dbName))
        {
            seedCtx.Trucks.Add(TruckTestFactory.NewValid(clock, codeRaw: "ALPHA01", nameRaw: "Alpha"));
            
            seedCtx.SaveChanges();
        }

        using ApplicationDbContext ctx = TestDbContextFactory.Create(dbName);
        ListTrucksHandler handler = new(ctx);
        ListTrucksQuery query = new(Page: 1, PageSize: 10, StatusFilter: null);

        // Act
        Result<PagedListDto<TruckSummaryDto>> result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should()
                        .BeTrue();
        result.Value!.TotalCount.Should()
                                .Be(1);
        result.Value.Items.Should()
                          .ContainSingle().Which.Code.Should()
                                                     .Be("ALPHA01");
    }

    [Fact]
    public async Task HandleAsync_filters_by_status_when_StatusFilter_is_set()
    {
        // Arrange
        string dbName = Guid.NewGuid().ToString();
        FakeDateTimeProvider clock = new(T0);

        using (ApplicationDbContext seedCtx = TestDbContextFactory.Create(dbName))
        {
            seedCtx.Trucks.Add(TruckTestFactory.NewValid(clock, codeRaw: "OOS01", initialStatus: ETruckStatus.OutOfService));
            seedCtx.Trucks.Add(TruckTestFactory.NewValid(clock, codeRaw: "LOAD01", initialStatus: ETruckStatus.Loading));
            
            seedCtx.SaveChanges();
        }

        using ApplicationDbContext ctx = TestDbContextFactory.Create(dbName);
        ListTrucksHandler handler = new(ctx);
        ListTrucksQuery query = new(Page: 1, PageSize: 10, StatusFilter: ETruckStatus.OutOfService);

        // Act
        Result<PagedListDto<TruckSummaryDto>> result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should()
                        .BeTrue();
        result.Value!.TotalCount.Should()
                                .Be(1);
        result.Value.Items.Should()
                          .ContainSingle().Which.Status.Should()
                                                       .Be((int)(ETruckStatus.OutOfService));
    }

    [Fact]
    public async Task HandleAsync_does_not_include_soft_deleted_trucks()
    {
        // Arrange
        string dbName = Guid.NewGuid()
                            .ToString();
        FakeDateTimeProvider clock = new(T0);

        using (ApplicationDbContext seedCtx = TestDbContextFactory.Create(dbName))
        {
            Truck active  = TruckTestFactory.NewValid(clock, codeRaw: "ACTIVE1");
            Truck deleted = TruckTestFactory.NewValid(clock, codeRaw: "DELETE1");
            
            deleted.Delete(clock, Guid.NewGuid());
            
            seedCtx.Trucks.Add(active);
            seedCtx.Trucks.Add(deleted);
            
            seedCtx.SaveChanges();
        }

        using ApplicationDbContext ctx = TestDbContextFactory.Create(dbName);
        ListTrucksHandler handler = new(ctx);
        ListTrucksQuery query = new(Page: 1, PageSize: 10, StatusFilter: null);

        // Act
        Result<PagedListDto<TruckSummaryDto>> result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should()
                        .BeTrue();
        result.Value!.TotalCount.Should()
                                .Be(1);
        result.Value.Items.Should()
                          .ContainSingle().Which.Code.Should()
                                                     .Be("ACTIVE1");
    }

    [Fact]
    public async Task HandleAsync_respects_page_and_page_size()
    {
        // Arrange
        string dbName = Guid.NewGuid()
                            .ToString();
        FakeDateTimeProvider clock = new(T0);

        using (ApplicationDbContext seedCtx = TestDbContextFactory.Create(dbName))
        {
            for (int i = 1; i <= 3; i++)
                seedCtx.Trucks.Add(TruckTestFactory.NewValid(clock, codeRaw: $"T{i:D3}"));
            
            seedCtx.SaveChanges();
        }

        using ApplicationDbContext ctx = TestDbContextFactory.Create(dbName);
        ListTrucksHandler handler = new(ctx);
        ListTrucksQuery query = new(Page: 1, PageSize: 2, StatusFilter: null);

        // Act
        Result<PagedListDto<TruckSummaryDto>> result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should()
                        .BeTrue();
        result.Value!.TotalCount.Should()
                                .Be(3);
        result.Value.Items.Count.Should()
                                .Be(2);
        result.Value.Page.Should()
                         .Be(1);
        result.Value.PageSize.Should()
                             .Be(2);
    }
}
