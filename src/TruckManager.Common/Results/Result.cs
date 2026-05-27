namespace TruckManager.Common.Results;

// [ADR-0039]   Implements IResult so pipeline behaviors can read IsSuccess uniformly across Result and Result<T>.
public class Result : IResult
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public IReadOnlyList<Error> Errors { get; }

    protected Result(bool isSuccess, IReadOnlyList<Error> errors)
    {
        // Defence against bugs, slightly redundant as constructor is protected and won't be called directly, but through factory methods. Better safe than sorry, innit?
        if (IsInvalid_SuccessWithErrors(isSuccess, errors.Count))
            throw new ArgumentException("A successful Result cannot carry errors.", nameof(errors));

        if (IsInvalid_FailureWithoutErrors(isSuccess, errors.Count))
            throw new ArgumentException("A failed Result must carry at least one error.", nameof(errors));
        
        IsSuccess = isSuccess;
        Errors = errors;
    }

    public static Result Success() => new(true, new List<Error>(1));
    public static Result Failure(Error error) => new(false, [error]);
    public static Result Failure(IEnumerable<Error> errors) => new(false, errors.ToList());

    // implicit cast to allow returning error directly
    public static implicit operator Result(Error error) => Result.Failure(error);

    private static bool IsInvalid_SuccessWithErrors(bool isSuccess, int errorCount) => isSuccess && errorCount > 0;
    private static bool IsInvalid_FailureWithoutErrors(bool isSuccess, int errorCount) => !isSuccess && errorCount <= 0;
}

public sealed class Result<T> : Result, IResult
{
    public T? Value { get; }

    private Result(bool isSuccess, T? value, IReadOnlyList<Error> errors) : base(isSuccess, errors)
    {
        Value = value;
    }

    public static Result<T> Success(T value) => new(true, value, new List<Error>(1));
    public static new Result<T> Failure(Error error) => new(false, default, [error]);
    public static new Result<T> Failure(IEnumerable<Error> errors) => new(false, default, errors.ToList());
    
    // implicit casting to allow returning values or errors directly
    public static implicit operator Result<T>(T value) => Result<T>.Success(value);

    public static implicit operator Result<T>(Error error) => Result<T>.Failure(error);
}
