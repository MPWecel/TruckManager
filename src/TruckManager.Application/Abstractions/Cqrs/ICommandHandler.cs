using TruckManager.Common.Results;

namespace TruckManager.Application.Abstractions.Cqrs;

// [ADR-0038]   Handler for a resultless command. Returns Result.Success / Result.Failure.
// The handler MUST NOT call SaveChangesAsync: the UnitOfWorkBehavior (see[ADR-0039]) commits on Result.Success and rolls back on Result.Failure (or when an exception is thrown).
public interface ICommandHandler<TCommand> where TCommand : ICommand
{
    Task<Result> HandleAsync(TCommand command, CancellationToken cancellationToken);
}

// [ADR-0038]   Handler for a payload-returning command. TResult is the full Result<T>, not the raw T, so the failure shape stays consistent.
public interface ICommandHandler<TCommand, TResult> where TCommand : ICommand<TResult>
{
    Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken);
}
