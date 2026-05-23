using AwesomeAssertions;
using TruckManager.Common.Results;
using Xunit;

namespace TruckManager.UnitTests.Application.Tests.Results;

public class ResultTests
{
    private static readonly Error SampleError = new("test.code", "test message", EErrorType.Validation);
    private static readonly Error AnotherError = new("test.other", "another message", EErrorType.NotFound);

    // ---- Result (non-generic) --------------------------------------------------

    [Fact]
    public void Success_yields_successful_result_with_no_errors()
    {
        //Arrange
        Result result = Result.Success();

        //Assert
        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Failure_with_single_error_yields_failed_result_carrying_that_error()
    {
        //Arrange
        Result result = Result.Failure(SampleError);

        //Assert
        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Should().Be(SampleError);
    }

    [Fact]
    public void Failure_with_multiple_errors_carries_all_of_them_in_order()
    {
        //Arrange
        Result result = Result.Failure([SampleError, AnotherError]);

        //Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().HaveCount(2);
        result.Errors[0].Should().Be(SampleError);
        result.Errors[1].Should().Be(AnotherError);
    }

    [Fact]
    public void Failure_with_empty_error_collection_throws()
    {
        //Arrange
        Action act = () => Result.Failure(Array.Empty<Error>());

        //Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Implicit_conversion_from_error_yields_failed_result()
    {
        //Arrange
        Result result = SampleError;

        //Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Should().Be(SampleError);
    }

    // ---- Result<T> -------------------------------------------------------------

    [Fact]
    public void Success_T_carries_value_and_is_successful()
    {
        //Arrange
        Result<int> result = Result<int>.Success(42);

        //Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Failure_T_with_single_error_carries_default_value_and_the_error()
    {
        //Arrange
        Result<int> result = Result<int>.Failure(SampleError);

        //Assert
        result.IsFailure.Should().BeTrue();
        result.Value.Should().Be(default(int));
        result.Errors.Should().ContainSingle().Which.Should().Be(SampleError);
    }

    [Fact]
    public void Failure_T_with_empty_error_collection_throws()
    {
        //Arrange
        Action act = () => Result<int>.Failure(Array.Empty<Error>());

        //Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Implicit_conversion_from_value_yields_successful_result_of_T()
    {
        //Arrange
        Result<string> result = "hello";

        //Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("hello");
    }

    [Fact]
    public void Implicit_conversion_from_error_yields_failed_result_of_T()
    {
        //Arrange
        Result<string> result = SampleError;

        //Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Should().Be(SampleError);
    }

    // ---- ResultExtensions ------------------------------------------------------

    [Fact]
    public void Map_transforms_successful_value()
    {
        //Arrange
        Result<int> input = Result<int>.Success(5);

        //Act
        Result<string> mapped = input.Map(x => x.ToString(System.Globalization.CultureInfo.InvariantCulture));

        //Assert
        mapped.IsSuccess.Should().BeTrue();
        mapped.Value.Should().Be("5");
    }

    [Fact]
    public void Map_short_circuits_on_failure_and_does_not_invoke_mapper()
    {
        //Arrange
        Result<int> input = Result<int>.Failure(SampleError);
        bool mapperInvoked = false;

        //Act
        Result<string> mapped = input.Map(
                                             x =>
                                             {
                                                 mapperInvoked = true;
                                                 return x.ToString(System.Globalization.CultureInfo.InvariantCulture);
                                             }
                                         );

        //Assert
        mapped.IsFailure.Should().BeTrue();
        mapped.Errors.Should().ContainSingle().Which.Should().Be(SampleError);
        mapperInvoked.Should().BeFalse();
    }

    [Fact]
    public void Bind_chains_successful_results()
    {
        //Arrange
        Result<int> input = Result<int>.Success(5);

        //Act
        Result<string> bound = input.Bind(x => Result<string>.Success($"value:{x}"));

        //Assert
        bound.IsSuccess.Should().BeTrue();
        bound.Value.Should().Be("value:5");
    }

    [Fact]
    public void Bind_short_circuits_on_failure_and_does_not_invoke_binder()
    {
        //Arrange
        Result<int> input = Result<int>.Failure(SampleError);
        bool binderInvoked = false;

        //Act
        Result<string> bound = input.Bind(
                                             x =>
                                             {
                                                 binderInvoked = true;
                                                 return Result<string>.Success(x.ToString(System.Globalization.CultureInfo.InvariantCulture));
                                             }
                                         );

        //Assert
        bound.IsFailure.Should().BeTrue();
        bound.Errors.Should().ContainSingle().Which.Should().Be(SampleError);
        binderInvoked.Should().BeFalse();
    }

    [Fact]
    public void Bind_propagates_failure_from_the_binder()
    {
        //Arrange
        Result<int> input = Result<int>.Success(5);

        //Act
        Result<string> bound = input.Bind(_ => Result<string>.Failure(AnotherError));

        //Assert
        bound.IsFailure.Should().BeTrue();
        bound.Errors.Should().ContainSingle().Which.Should().Be(AnotherError);
    }

    [Fact]
    public void Tap_invokes_action_on_success_and_returns_original_result()
    {
        //Arrange
        Result<int> input = Result<int>.Success(7);
        int captured = 0;

        //Act
        Result<int> output = input.Tap(x => captured = x);

        //Assert
        output.Should().BeSameAs(input);
        captured.Should().Be(7);
    }

    [Fact]
    public void Tap_does_not_invoke_action_on_failure()
    {
        //Arrange
        Result<int> input = Result<int>.Failure(SampleError);
        bool actionInvoked = false;

        //Act
        Result<int> output = input.Tap(_ => actionInvoked = true);

        //Assert
        output.Should().BeSameAs(input);
        actionInvoked.Should().BeFalse();
    }

    [Fact]
    public void Match_returns_success_branch_when_successful()
    {
        //Arrange
        Result<int> input = Result<int>.Success(3);

        //Act
        string output = input.Match(
                                       onSuccess: x => $"ok:{x}",
                                       onFailure: errors => $"err:{errors.Count}"
                                   );

        //Assert
        output.Should().Be("ok:3");
    }

    [Fact]
    public void Match_returns_failure_branch_when_failed()
    {
        //Arrange
        Result<int> input = Result<int>.Failure([SampleError, AnotherError]);

        //Act
        string output = input.Match(
                                       onSuccess: x => $"ok:{x}",
                                       onFailure: errors => $"err:{errors.Count}"
                                   );

        //Assert
        output.Should().Be("err:2");
    }

    [Fact]
    public void Combine_returns_success_when_all_results_are_successful()
    {
        //Arrange + Act
        Result combined = ResultExtensions.Combine(Result.Success(), Result.Success(), Result.Success());

        //Assert
        combined.IsSuccess.Should().BeTrue();
        combined.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Combine_aggregates_errors_from_all_failed_results()
    {
        //Arrange + Act
        Result combined = ResultExtensions.Combine(
                                                      Result.Success(),
                                                      Result.Failure(SampleError),
                                                      Result.Failure(AnotherError)
                                                  );

        //Assert
        combined.IsFailure.Should().BeTrue();
        combined.Errors.Should().HaveCount(2);
        combined.Errors.Should().Contain(SampleError);
        combined.Errors.Should().Contain(AnotherError);
    }

    // ---- Error record ----------------------------------------------------------

    [Fact]
    public void Errors_with_identical_fields_are_equal_via_record_semantics()
    {
        //Arrange
        Error a = new("code", "msg", EErrorType.NotFound);
        Error b = new("code", "msg", EErrorType.NotFound);

        //Assert
        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Errors_with_different_type_are_not_equal()
    {
        //Arrange
        Error a = new("code", "msg", EErrorType.NotFound);
        Error b = new("code", "msg", EErrorType.Conflict);

        //Assert
        a.Should().NotBe(b);
    }
}
