using AwesomeAssertions;
using FluentValidation;
using Xunit;

using TruckManager.Common.Results;
using TruckManager.Application.Abstractions.Cqrs;
using TruckManager.Application.Behaviors;

namespace TruckManager.UnitTests.Application.Tests.Behaviors;

public class ValidationBehaviorTests
{
    // ---- Test types -------------------------------------------------------

    private sealed record TestCommand(string Value) : ICommand;

    private sealed class RequireValueValidator : AbstractValidator<TestCommand>
    {
        public RequireValueValidator() => RuleFor(x => x.Value).NotEmpty();
    }

    // ---- Helpers ----------------------------------------------------------

    private static ValidationBehavior<TestCommand, Result> BuildBehavior(IEnumerable<IValidator<TestCommand>> validators) 
        => new(validators);

    // ---- No validators ----------------------------------------------------

    [Fact]
    public async Task When_no_validators_are_registered_next_is_called_and_result_is_returned()
    {
        // Arrange
        ValidationBehavior<TestCommand, Result> behavior = BuildBehavior([]);
        TestCommand command = new("anything");
        bool nextCalled = false;

        Func<Task<Result>> next = () =>
                                  {
                                      nextCalled = true;
                                      return Task.FromResult(Result.Success());
                                  };

        // Act
        Result result = await behavior.HandleAsync(command, next, CancellationToken.None);

        // Assert
        nextCalled.Should()
                  .BeTrue();
        result.IsSuccess.Should()
                        .BeTrue();
    }

    // ---- Valid request ----------------------------------------------------

    [Fact]
    public async Task When_request_is_valid_next_is_called_and_handler_result_is_returned()
    {
        // Arrange
        ValidationBehavior<TestCommand, Result> behavior = BuildBehavior([new RequireValueValidator()]);
        TestCommand command = new("non-empty");
        bool nextCalled = false;

        Func<Task<Result>> next = () =>
                                  {
                                      nextCalled = true;
                                      return Task.FromResult(Result.Success());
                                  };

        // Act
        Result result = await behavior.HandleAsync(command, next, CancellationToken.None);

        // Assert
        nextCalled.Should()
                  .BeTrue();
        result.IsSuccess.Should()
                        .BeTrue();
    }

    // ---- Invalid request --------------------------------------------------

    [Fact]
    public async Task When_request_is_invalid_next_is_NOT_called_and_Failure_is_returned()
    {
        // Arrange
        ValidationBehavior<TestCommand, Result> behavior = BuildBehavior([new RequireValueValidator()]);
        TestCommand command = new("");   // triggers NotEmpty
        bool nextCalled = false;

        Func<Task<Result>> next = () =>
                                  {
                                      nextCalled = true;
                                      return Task.FromResult(Result.Success());
                                  };

        // Act
        Result result = await behavior.HandleAsync(command, next, CancellationToken.None);

        // Assert
        nextCalled.Should()
                  .BeFalse();
        result.IsSuccess.Should()
                        .BeFalse();
        result.Errors.Should()
                     .NotBeEmpty();
    }

    [Fact]
    public async Task Validation_errors_carry_EErrorType_Validation()
    {
        // Arrange
        ValidationBehavior<TestCommand, Result> behavior = BuildBehavior([new RequireValueValidator()]);
        TestCommand command = new("");

        // Act
        Result result = await behavior.HandleAsync(
                                                      command, 
                                                      () => Task.FromResult(Result.Success()), 
                                                      CancellationToken.None
                                                  );

        // Assert
        result.Errors.Should()
                     .AllSatisfy(
                                    e => e.Type.Should()
                                               .Be(EErrorType.Validation)
                                );
    }

    [Fact]
    public async Task Multiple_validators_all_run_and_all_errors_are_collected()
    {
        // Arrange — two validators, both fail on an empty value
        var validators = new IValidator<TestCommand>[]
        {
            new RequireValueValidator(),
            new RequireValueValidator()
        };
        ValidationBehavior<TestCommand, Result> behavior = BuildBehavior(validators);
        TestCommand command = new("");

        // Act
        Result result = await behavior.HandleAsync(
                                                      command, 
                                                      () => Task.FromResult(Result.Success()), 
                                                      CancellationToken.None
                                                  );

        // Assert — both validators contribute an error
        result.Errors.Count.Should()
                           .BeGreaterThanOrEqualTo(2);
    }
}
