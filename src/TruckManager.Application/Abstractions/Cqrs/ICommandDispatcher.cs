using TruckManager.Common.Results;

namespace TruckManager.Application.Abstractions.Cqrs;

// [ADR-0038]   Entry point for the command side of the CQRS pipeline.
// Callers inject this and call SendAsync(command); Dispatcher resolves the matching ICommandHandler<> from DI and composes registered IPipelineBehavior<> layers (including UnitOfWorkBehavior per [ADR-0039]) around it.
//
// Two overloads:
//   > SendAsync(ICommand, ct):                     resultless command, returns Result;
//   > SendAsync<TResult>(ICommand<TResult>,ct):    payload-returning command;
// Implementations in TruckManager.Application.Cqrs.CommandDispatcher.
public interface ICommandDispatcher
{
    Task<Result> SendAsync(ICommand command, CancellationToken cancellationToken);
    Task<TResult> SendAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken);
}
