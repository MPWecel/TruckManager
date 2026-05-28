using Microsoft.Extensions.Logging;
using AwesomeAssertions;
using Xunit;

using TruckManager.Application.Behaviors;
using TruckManager.Common.Results;
using TruckManager.UnitTests.TestHelpers;

namespace TruckManager.UnitTests.Application.Tests.Behaviors;

// Phase 7 / Section C.   Unit tests for LoggingBehavior — covers the four log paths plus the "result and exception propagate unchanged" contract.
public class LoggingBehaviorTests
{
    //HappyPath

    [Fact]
    public async Task HandleAsync_returns_inner_result_unchanged_on_success()
    {
        CapturingLogger<LoggingBehavior<FakeRequest, Result>> logger = new();
        LoggingBehavior<FakeRequest, Result> behavior = new(logger);
        Result successResult = Result.Success();

        Result returned = await behavior.HandleAsync(
                                                        new FakeRequest(), 
                                                        () => Task.FromResult(successResult), 
                                                        TestContext.Current.CancellationToken
                                                    );

        returned.Should()
                .BeSameAs(successResult);
    }

    [Fact]
    public async Task HandleAsync_logs_Debug_starting_and_Information_succeeded_on_success()
    {
        CapturingLogger<LoggingBehavior<FakeRequest, Result>> logger = new();
        LoggingBehavior<FakeRequest, Result> behavior = new(logger);

        await behavior.HandleAsync(
                                      new FakeRequest(), 
                                      () => Task.FromResult(Result.Success()), 
                                      TestContext.Current.CancellationToken
                                  );

        logger.Entries.Should()
                      .Contain(e => e.Level == LogLevel.Debug && e.Message.Contains("FakeRequest") && e.Message.Contains("starting"));
        logger.Entries.Should()
                      .Contain(e => e.Level == LogLevel.Information && e.Message.Contains("FakeRequest") && e.Message.Contains("succeeded"));
        logger.Entries.Should()
                      .NotContain(e => e.Level == LogLevel.Warning);
    }

    //FailurePaths

    [Fact]
    public async Task HandleAsync_returns_inner_failure_unchanged()
    {
        CapturingLogger<LoggingBehavior<FakeRequest, Result>> logger = new();
        LoggingBehavior<FakeRequest, Result> behavior = new(logger);
        Error error = new("test.boom", "Test failure", EErrorType.Validation);
        Result failure = Result.Failure(error);

        Result returned = await behavior.HandleAsync(
                                                        new FakeRequest(), 
                                                        () => Task.FromResult(failure), 
                                                        TestContext.Current.CancellationToken
                                                    );

        returned.Should()
                .BeSameAs(failure);
    }

    [Fact]
    public async Task HandleAsync_logs_Warning_with_error_count_and_first_code_on_failure()
    {
        CapturingLogger<LoggingBehavior<FakeRequest, Result>> logger = new();
        LoggingBehavior<FakeRequest, Result> behavior = new(logger);
        Error error = new("test.boom", "Test failure", EErrorType.Validation);

        await behavior.HandleAsync(
                                      new FakeRequest(), 
                                      () => Task.FromResult(Result.Failure(error)), 
                                      TestContext.Current.CancellationToken
                                  );

        logger.Entries.Should()
                      .Contain(
                                  e => 
                                    e.Level == LogLevel.Warning && 
                                    e.Message.Contains("FakeRequest") && 
                                    e.Message.Contains("failed") && 
                                    e.Message.Contains("test.boom")
                              );
        logger.Entries.Should()
                      .NotContain(e => e.Level == LogLevel.Information && e.Message.Contains("succeeded"));
    }

    //ExceptionPaths

    [Fact]
    public async Task HandleAsync_rethrows_exceptions_from_next()
    {
        CapturingLogger<LoggingBehavior<FakeRequest, Result>> logger = new();
        LoggingBehavior<FakeRequest, Result> behavior = new(logger);
        InvalidOperationException boom = new("kaboom");

        InvalidOperationException thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => behavior.HandleAsync(new FakeRequest(), () => throw boom, TestContext.Current.CancellationToken)
        );

        thrown.Should()
              .BeSameAs(boom);
    }

    [Fact]
    public async Task HandleAsync_logs_Error_with_exception_on_throw()
    {
        CapturingLogger<LoggingBehavior<FakeRequest, Result>> logger = new();
        LoggingBehavior<FakeRequest, Result> behavior = new(logger);
        InvalidOperationException boom = new("kaboom");

        try
        {
            await behavior.HandleAsync(new FakeRequest(), () => throw boom, TestContext.Current.CancellationToken);
        }
        catch (InvalidOperationException)
        {
            // Expected — the behavior re-throws.
        }

        logger.Entries.Should()
                      .Contain(
                                  e => 
                                    e.Level == LogLevel.Error && 
                                    e.Exception == boom && 
                                    e.Message.Contains("FakeRequest") && 
                                    e.Message.Contains("threw") && 
                                    e.Message.Contains(nameof(InvalidOperationException))
                              );
    }

    //GenericResult<T>Paths

    [Fact]
    public async Task HandleAsync_works_for_typed_Result_on_success()
    {
        CapturingLogger<LoggingBehavior<FakeRequest, Result<int>>> logger = new();
        LoggingBehavior<FakeRequest, Result<int>> behavior = new(logger);

        Result<int> returned = await behavior.HandleAsync(
                                                             new FakeRequest(),
                                                             () => Task.FromResult(Result<int>.Success(42)),
                                                             TestContext.Current.CancellationToken
                                                         );

        returned.IsSuccess.Should()
                          .BeTrue();
        returned.Value.Should()
                      .Be(42);
        logger.Entries.Should()
                      .Contain(e => e.Level == LogLevel.Information && e.Message.Contains("succeeded"));
    }

    [Fact]
    public async Task HandleAsync_works_for_typed_Result_on_failure()
    {
        CapturingLogger<LoggingBehavior<FakeRequest, Result<int>>> logger = new();
        LoggingBehavior<FakeRequest, Result<int>> behavior = new(logger);
        Error error = new("typed.boom", "Typed failure", EErrorType.NotFound);

        Result<int> returned = await behavior.HandleAsync(
                                                             new FakeRequest(),
                                                             () => Task.FromResult(Result<int>.Failure(error)),
                                                             TestContext.Current.CancellationToken
                                                         );

        returned.IsSuccess.Should()
                          .BeFalse();
        logger.Entries.Should()
                      .Contain(e => e.Level == LogLevel.Warning && e.Message.Contains("typed.boom"));
    }

    private sealed record FakeRequest;

}
