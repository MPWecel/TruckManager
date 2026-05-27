using AwesomeAssertions;
using Xunit;

using TruckManager.Common.Results;
using TruckManager.Application.Abstractions.Cqrs;
using TruckManager.Application.Behaviors;
using TruckManager.UnitTests.TestHelpers;

namespace TruckManager.UnitTests.Application.Tests.Behaviors;

public class UnitOfWorkBehaviorTests
{
    // ---- Test types -------------------------------------------------------

    private sealed record TestCommand(string Value) : ICommand;

    // ---- Helpers ----------------------------------------------------------

    private static (UnitOfWorkBehavior<TestCommand, Result> behavior, FakeUnitOfWork uow) Build()
    {
        FakeUnitOfWork uow = new();
        UnitOfWorkBehavior<TestCommand, Result> behavior = new(uow);
        return (behavior, uow);
    }

    // ---- Success path: Begin → next → SaveChanges → Commit ---------------

    [Fact]
    public async Task On_success_result_BeginTransaction_SaveChanges_Commit_are_called()
    {
        // Arrange
        var (behavior, uow) = Build();
        TestCommand command = new("ok");

        // Act
        Result result = await behavior.HandleAsync(
                                                      command,
                                                      () => Task.FromResult(Result.Success()),
                                                      CancellationToken.None
                                                  );

        // Assert
        result.IsSuccess.Should()
                        .BeTrue();

        uow.BeginTransactionCalls.Should()
                                 .Be(1);
        uow.SaveChangesCalls.Should()
                            .Be(1);
        uow.CommitCalls.Should()
                       .Be(1);
        uow.RollbackCalls.Should()
                         .Be(0);
    }

    // ---- Failure path: Begin → next → Rollback (NO SaveChanges/Commit) ---

    [Fact]
    public async Task On_failure_result_BeginTransaction_and_Rollback_are_called_but_NOT_Commit()
    {
        // Arrange
        var (behavior, uow) = Build();
        TestCommand command = new("fail");
        Error failureError = new("test.error", "Simulated handler failure.", EErrorType.Validation);

        // Act
        Result result = await behavior.HandleAsync(
                                                      command,
                                                      () => Task.FromResult(Result.Failure(failureError)),
                                                      CancellationToken.None
                                                  );

        // Assert
        result.IsSuccess.Should()
                        .BeFalse();

        uow.BeginTransactionCalls.Should()
                                 .Be(1);
        uow.RollbackCalls.Should()
                         .Be(1);
        uow.SaveChangesCalls.Should()
                            .Be(0);
        uow.CommitCalls.Should()
                       .Be(0);
    }

    [Fact]
    public async Task Commit_and_Rollback_are_never_both_called_for_success()
    {
        // Arrange
        var (behavior, uow) = Build();
        TestCommand command = new("ok");

        // Act
        await behavior.HandleAsync(
                                      command, 
                                      () => Task.FromResult(Result.Success()), 
                                      CancellationToken.None
                                  );

        // Assert
        int exclusiveCallCount = uow.CommitCalls + uow.RollbackCalls;
        exclusiveCallCount.Should()
                          .Be(1);
        uow.RollbackCalls.Should()
                         .Be(0);
    }

    [Fact]
    public async Task Commit_and_Rollback_are_never_both_called_for_failure()
    {
        // Arrange
        var (behavior, uow) = Build();
        TestCommand command = new("fail");
        Error error = new("e", "msg", EErrorType.NotFound);

        // Act
        await behavior.HandleAsync(
                                      command, 
                                      () => Task.FromResult(Result.Failure(error)), 
                                      CancellationToken.None
                                  );

        // Assert
        int exclusiveCallCount = uow.CommitCalls + uow.RollbackCalls;
        exclusiveCallCount.Should()
                          .Be(1);
        uow.CommitCalls.Should()
                       .Be(0);
    }
}
