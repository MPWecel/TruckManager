using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

using TruckManager.Common.Results;
using TruckManager.Domain.Enums;
using TruckManager.Domain.ValueObjects;
using TruckManager.Application.Abstractions.Cqrs;
using TruckManager.Application.Trucks.Commands.CreateTruck;
using TruckManager.Application.Trucks.DTOs;
using TruckManager.Application.Trucks.Queries.GetTruckById;
using TruckManager.Application.Trucks.Queries.ListTrucks;
using TruckManager.IntegrationTests.TestHelpers;

namespace TruckManager.IntegrationTests.Application;

// Phase 5 / Section G.
//
// Query pipeline smoke tests (Testcontainers-backed Postgres + production DI graph).
// Exercises: Dispatcher → ValidationBehavior → Handler → EF Core projection → real Postgres.
// UnitOfWorkBehavior is NOT wired into the query pipeline (IBaseCommand constraint).
//
// V1 note: full SQL-level projection verification (asserting only DTO columns are SELECTed) is deferred to Phase 8 architecture tests.
// These tests confirm correctness of the returned data and that no exceptions occur during projection evaluation against real Postgres.
public sealed class QueryPipelineTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture = fixture;

    private static string UniqueCode() => Guid.NewGuid()
                                              .ToString("N")
                                              .ToUpperInvariant()[..8];

    // ---- GetTruckById -----------------------------------------------------

    [Fact]
    public async Task GetTruckById_returns_correct_TruckDto_for_existing_truck()
    {
        // Arrange — seed a truck
        string code = UniqueCode();
        Guid truckId = await CreateTruckAsync(code: code, name: "Query Target", status: ETruckStatus.Loading);

        using IServiceScope scope = _fixture.Services.CreateScope();
        IQueryDispatcher queryDispatcher = scope.ServiceProvider.GetRequiredService<IQueryDispatcher>();

        // Act
        Result<TruckDto> result = await queryDispatcher.SendAsync(new GetTruckByIdQuery(truckId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should()
                        .BeTrue();
        TruckDto dto = result.Value!;
        dto.Id.Should()
              .Be(truckId);
        dto.Code.Should()
                .Be(code);
        dto.Name.Should()
                .Be("Query Target");
        dto.Status.Should()
                  .Be((int)(ETruckStatus.Loading));
        dto.IsDeleted.Should()
                     .BeFalse();
        dto.CreatedAtUtc.Should()
                        .NotBe(default);
        dto.Version.Should()
                   .BeGreaterThan(0);
    }

    [Fact]
    public async Task GetTruckById_returns_NotFound_for_unknown_id()
    {
        // Arrange
        using IServiceScope scope = _fixture.Services.CreateScope();
        IQueryDispatcher queryDispatcher = scope.ServiceProvider.GetRequiredService<IQueryDispatcher>();

        // Act
        Result<TruckDto> result = await queryDispatcher.SendAsync(new GetTruckByIdQuery(Guid.NewGuid()), CancellationToken.None);

        // Assert
        result.IsSuccess.Should()
                        .BeFalse();
        result.Errors.Should()
                     .ContainSingle()
                     .Which.Type.Should()
                                .Be(EErrorType.NotFound);
    }

    [Fact]
    public async Task GetTruckById_with_empty_id_returns_Validation_failure_without_hitting_database()
    {
        // Arrange
        using IServiceScope scope = _fixture.Services.CreateScope();
        IQueryDispatcher queryDispatcher = scope.ServiceProvider.GetRequiredService<IQueryDispatcher>();

        // Act — ValidationBehavior intercepts before the handler runs
        Result<TruckDto> result = await queryDispatcher.SendAsync(new GetTruckByIdQuery(Guid.Empty),CancellationToken.None);

        // Assert
        result.IsSuccess.Should()
                        .BeFalse();
        result.Errors.Should()
                     .Contain(e => e.Type == EErrorType.Validation);
    }

    // ---- ListTrucks -------------------------------------------------------

    [Fact]
    public async Task ListTrucks_returns_paged_result_with_all_active_trucks_matching_filter()
    {
        // Arrange — seed two trucks with distinct statuses under a shared unique tenant slice (we can't isolate by TenantId in the query; we just verify our seeded trucks appear)
        Guid tenantId = Guid.NewGuid();
        string codeOos  = UniqueCode();
        string codeLoad = UniqueCode();

        await CreateTruckAsync(codeOos, "OOS Truck", ETruckStatus.OutOfService, tenantId);
        await CreateTruckAsync(codeLoad, "Loading Truck", ETruckStatus.Loading, tenantId);

        using IServiceScope scope = _fixture.Services.CreateScope();
        IQueryDispatcher queryDispatcher = scope.ServiceProvider.GetRequiredService<IQueryDispatcher>();

        // Act — filter by OutOfService; only the first truck should match
        Result<PagedListDto<TruckSummaryDto>> result = 
            await queryDispatcher.SendAsync(new ListTrucksQuery(Page: 1, PageSize: 50, StatusFilter: ETruckStatus.OutOfService),CancellationToken.None);

        // Assert
        result.IsSuccess.Should()
                        .BeTrue();
        result.Value!.Items.Should()
                           .Contain(dto => dto.Code == codeOos);
        result.Value.Items.Should()
                          .NotContain(dto => dto.Code == codeLoad);
    }

    [Fact]
    public async Task ListTrucks_returns_correct_page_metadata()
    {
        // Arrange — seed 3 trucks; request page 1 with size 2; expect TotalCount ≥ 3
        string code1 = UniqueCode();
        string code2 = UniqueCode();
        string code3 = UniqueCode();
        
        await CreateTruckAsync(code1, "Page Test 1", ETruckStatus.OutOfService);
        await CreateTruckAsync(code2, "Page Test 2", ETruckStatus.OutOfService);
        await CreateTruckAsync(code3, "Page Test 3", ETruckStatus.OutOfService);

        using IServiceScope scope = _fixture.Services.CreateScope();
        IQueryDispatcher queryDispatcher = scope.ServiceProvider.GetRequiredService<IQueryDispatcher>();

        // Act
        Result<PagedListDto<TruckSummaryDto>> result = 
            await queryDispatcher.SendAsync(new ListTrucksQuery(Page: 1, PageSize: 2, StatusFilter: null),CancellationToken.None);

        // Assert
        result.IsSuccess.Should()
                        .BeTrue();
        result.Value!.Page.Should()
                          .Be(1);
        result.Value.PageSize.Should()
                             .Be(2);
        result.Value.Items.Count.Should()
                                .Be(2);
        result.Value.TotalCount.Should()
                               .BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task ListTrucks_with_invalid_page_returns_Validation_failure()
    {
        // Arrange
        using IServiceScope scope = _fixture.Services.CreateScope();
        IQueryDispatcher queryDispatcher = scope.ServiceProvider.GetRequiredService<IQueryDispatcher>();

        // Act — page 0 violates the GreaterThanOrEqualTo(1) rule
        Result<PagedListDto<TruckSummaryDto>> result = 
            await queryDispatcher.SendAsync(new ListTrucksQuery(Page: 0, PageSize: 10, StatusFilter: null), CancellationToken.None);

        // Assert
        result.IsSuccess.Should()
                        .BeFalse();
        result.Errors.Should()
                     .Contain(e => e.Type == EErrorType.Validation);
    }

    // ---- Private helpers --------------------------------------------------

    private async Task<Guid> CreateTruckAsync(string code, string name, ETruckStatus status, Guid? tenantId = null)
    {
        using IServiceScope scope = _fixture.Services.CreateScope();
        ICommandDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher>();

        CreateTruckCommand command = new(
                                            TenantId: tenantId ?? Guid.NewGuid(),
                                            Code: code,
                                            Name: name,
                                            Description: null,
                                            InitialStatus: status
                                        );

        Result<TruckId> result = await dispatcher.SendAsync(command, CancellationToken.None);
        
        result.IsSuccess.Should()
                        .BeTrue("prerequisite truck creation must succeed");
        
        return result.Value!.Value;
    }
}
