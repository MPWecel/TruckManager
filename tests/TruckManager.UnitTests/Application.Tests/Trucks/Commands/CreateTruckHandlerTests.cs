using AwesomeAssertions;
using Xunit;

using TruckManager.Common.Results;
using TruckManager.Domain.Aggregates.Trucks;
using TruckManager.Domain.Enums;
using TruckManager.Domain.ValueObjects;
using TruckManager.Application.Trucks.Commands.CreateTruck;
using TruckManager.Infrastructure.Persistence;
using TruckManager.UnitTests.TestHelpers;

namespace TruckManager.UnitTests.Application.Tests.Trucks.Commands;

public class CreateTruckHandlerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 5, 13, 13, 37, 0, TimeSpan.Zero);

    private static CreateTruckHandler BuildHandler()
        => new(TestDbContextFactory.Create(), FakeCurrentUserService.Anonymous(), new FakeDateTimeProvider(T0), new FakeCorrelationContext());

    private static CreateTruckHandler BuildHandlerForDb(ApplicationDbContext ctx, FakeDateTimeProvider clock)
        => new(ctx, FakeCurrentUserService.Anonymous(), clock, new FakeCorrelationContext());

    // ---- Happy path -------------------------------------------------------

    [Fact]
    public async Task HandleAsync_returns_Success_with_a_non_empty_TruckId_for_valid_inputs()
    {
        // Arrange
        CreateTruckHandler handler = BuildHandler();
        CreateTruckCommand command = new(
                                            TenantId: Guid.NewGuid(),
                                            Code: "ALPHA01",
                                            Name: "Alpha Truck",
                                            Description: "A test truck",
                                            InitialStatus: ETruckStatus.OutOfService
                                        );

        // Act
        Result<TruckId> result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should()
                        .BeTrue();
        result.Value.Should()
                    .NotBeNull();
        result.Value!.Value.Should()
                           .NotBeEmpty();
    }

    [Fact]
    public async Task HandleAsync_null_description_is_accepted_and_maps_to_Empty()
    {
        // Arrange
        CreateTruckHandler handler = BuildHandler();
        CreateTruckCommand command = new(
                                            TenantId: Guid.NewGuid(),
                                            Code: "NODESC",
                                            Name: "No-description truck",
                                            Description: null,
                                            InitialStatus: ETruckStatus.OutOfService
                                        );

        // Act
        Result<TruckId> result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should()
                        .BeTrue();
    }

    // ---- Failure paths ----------------------------------------------------

    [Fact]
    public async Task HandleAsync_returns_Failure_when_code_contains_invalid_characters()
    {
        // Arrange
        CreateTruckHandler handler = BuildHandler();
        CreateTruckCommand command = new(
                                            TenantId:      Guid.NewGuid(),
                                            Code:          "truck-01",   // hyphens not allowed
                                            Name:          "Bad Code",
                                            Description:   null,
                                            InitialStatus: ETruckStatus.OutOfService
                                        );

        // Act
        Result<TruckId> result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should()
                        .BeFalse();
        result.Errors.Should()
                     .NotBeEmpty();
    }

    [Fact]
    public async Task HandleAsync_returns_Failure_when_code_is_whitespace()
    {
        // Arrange
        CreateTruckHandler handler = BuildHandler();
        CreateTruckCommand command = new(
                                            TenantId:      Guid.NewGuid(),
                                            Code:          "   ",
                                            Name:          "Whitespace code",
                                            Description:   null,
                                            InitialStatus: ETruckStatus.OutOfService
                                        );

        // Act
        Result<TruckId> result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should()
                        .BeFalse();
    }

    // ---- Duplicate-code pre-check (ADR-0033 partial-index UX_Trucks_TenantId_Code) -------

    [Fact]
    public async Task HandleAsync_returns_Conflict_when_code_already_exists_in_the_same_tenant()
    {
        // Arrange — seed the first truck through the same handler path so we exercise the
        // production code that adds to the change tracker (with an explicit SaveChanges since
        // unit tests run without UnitOfWorkBehavior).
        string                  dbName    = Guid.NewGuid().ToString();
        Guid                    tenantId  = Guid.NewGuid();
        FakeDateTimeProvider    clock     = new(T0);

        CreateTruckCommand firstCommand = new(
                                                 TenantId:      tenantId,
                                                 Code:          "DUPLICATE",
                                                 Name:          "First truck",
                                                 Description:   null,
                                                 InitialStatus: ETruckStatus.OutOfService
                                             );

        using (ApplicationDbContext firstCtx = TestDbContextFactory.Create(dbName))
        {
            CreateTruckHandler firstHandler = BuildHandlerForDb(firstCtx, clock);

            Result<TruckId> firstResult = await firstHandler.HandleAsync(firstCommand, CancellationToken.None);

            firstResult.IsSuccess.Should()
                                 .BeTrue();

            await firstCtx.SaveChangesAsync(TestContext.Current.CancellationToken);  // simulate UnitOfWorkBehavior commit
        }

        // Act — second create with the same (tenant, code) under a fresh context (same store).
        using ApplicationDbContext secondCtx = TestDbContextFactory.Create(dbName);
        CreateTruckHandler secondHandler = BuildHandlerForDb(secondCtx, clock);

        CreateTruckCommand secondCommand = new(
                                                  TenantId:      tenantId,
                                                  Code:          "DUPLICATE",
                                                  Name:          "Second truck",
                                                  Description:   null,
                                                  InitialStatus: ETruckStatus.OutOfService
                                              );

        Result<TruckId> secondResult = await secondHandler.HandleAsync(secondCommand, CancellationToken.None);

        // Assert
        secondResult.IsSuccess.Should()
                              .BeFalse();
        secondResult.Errors.Should()
                           .ContainSingle()
                           .Which.Type.Should()
                                      .Be(EErrorType.Conflict);
    }

    [Fact]
    public async Task HandleAsync_allows_same_code_across_different_tenants()
    {
        // Uniqueness scope is (TenantId, Code) — the same code in a different tenant must succeed.
        // This guards against an accidental tenancy-stripping change to the pre-check query.
        string                  dbName  = Guid.NewGuid().ToString();
        FakeDateTimeProvider    clock   = new(T0);
        Guid                    tenant1 = Guid.NewGuid();
        Guid                    tenant2 = Guid.NewGuid();

        using (ApplicationDbContext firstCtx = TestDbContextFactory.Create(dbName))
        {
            CreateTruckHandler firstHandler = BuildHandlerForDb(firstCtx, clock);
            CreateTruckCommand firstCommand = new(
                                                     TenantId:      tenant1,
                                                     Code:          "SHARED",
                                                     Name:          "Tenant1 truck",
                                                     Description:   null,
                                                     InitialStatus: ETruckStatus.OutOfService
                                                 );

            Result<TruckId> firstResult = await firstHandler.HandleAsync(firstCommand, CancellationToken.None);
            firstResult.IsSuccess.Should()
                                 .BeTrue();
            await firstCtx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using ApplicationDbContext secondCtx = TestDbContextFactory.Create(dbName);
        CreateTruckHandler secondHandler = BuildHandlerForDb(secondCtx, clock);
        CreateTruckCommand secondCommand = new(
                                                  TenantId:      tenant2,
                                                  Code:          "SHARED",
                                                  Name:          "Tenant2 truck",
                                                  Description:   null,
                                                  InitialStatus: ETruckStatus.OutOfService
                                              );

        Result<TruckId> secondResult = await secondHandler.HandleAsync(secondCommand, CancellationToken.None);

        secondResult.IsSuccess.Should()
                              .BeTrue();
    }

    // ---- Phase 7 / Section E   CorrelationId plumbing ----------------------------------

    [Fact]
    public async Task HandleAsync_stamps_CorrelationId_from_ICorrelationContext_onto_raised_event()
    {
        // Arrange — use a deterministic correlation id so we can compare it on the raised event.
        Guid                    expectedCorrelationId   = Guid.NewGuid();
        FakeDateTimeProvider    clock                   = new(T0);
        FakeCorrelationContext  correlation             = FakeCorrelationContext.WithId(expectedCorrelationId);

        using ApplicationDbContext ctx = TestDbContextFactory.Create();
        CreateTruckHandler handler = new(ctx, FakeCurrentUserService.Anonymous(), clock, correlation);
        CreateTruckCommand command = new(
                                            TenantId:      Guid.NewGuid(),
                                            Code:          "CORR01",
                                            Name:          "Correlated truck",
                                            Description:   null,
                                            InitialStatus: ETruckStatus.OutOfService
                                        );

        // Act
        Result<TruckId> result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        // Assert — handler succeeded.
        result.IsSuccess.Should()
                        .BeTrue();

        // Assert — the TruckCreated domain event raised by the aggregate carries the same CorrelationId.
        // Unit tests don't wire the DomainEventPersistenceInterceptor, so the event is still in
        // the aggregate's DomainEvents queue after the handler returns — perfect for assertion.
        Truck truck = ctx.Trucks.Local.Single();
        truck.DomainEvents.Should()
                          .ContainSingle()
                          .Which.CorrelationId.Should()
                                              .Be(expectedCorrelationId);
    }
}
