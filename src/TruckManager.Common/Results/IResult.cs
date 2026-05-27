namespace TruckManager.Common.Results;

// [ADR-0039]   Non-generic marker for both Result and Result<T>.
// Lets pipeline behaviors (specifically UnitOfWorkBehavior) inspect IsSuccess without closing over the generic TResult type:
// `result is IResult { IsSuccess: true }` works uniformly across the resultless and typed return shapes.
public interface IResult
{
    bool IsSuccess { get; }
}
