using TruckManager.Application.Abstractions.Cqrs;
using TruckManager.Application.Abstractions.Persistence;
using TruckManager.Common.Results;

namespace TruckManager.Application.Behaviors;

// [ADR-0039]   Wraps every command in a DB transaction.
// The where TCommand : IBaseCommand constraint restricts this behavior to command types (ICommand / ICommand<TResult>).
// The DI open-generic resolution skips it for IQuery<> types, so the transaction pipeline is absent from the query dispatcher automatically.
//
// Flow: Begin → next() → IResult.IsSuccess? SaveChanges + Commit : Rollback.
// Handlers must NOT call SaveChangesAsync themselves - that is this behavior's job.
//
// [Phase 7: LoggingBehavior can observe the rollback path independently via Serilog.]
public sealed class UnitOfWorkBehavior<TCommand, TResult> : IPipelineBehavior<TCommand, TResult> where TCommand : IBaseCommand
{
    private readonly IUnitOfWork _uow;

    public UnitOfWorkBehavior(IUnitOfWork uow)
    {
        ArgumentNullException.ThrowIfNull(uow);
        _uow = uow;
    }

    public async Task<TResult> HandleAsync(
                                              TCommand command,
                                              Func<Task<TResult>> next,
                                              CancellationToken cancellationToken
                                          )
    {
        await _uow.BeginTransactionAsync(cancellationToken);
        TResult result = await next();

        bool isSuccess = result is IResult { IsSuccess: true };
        if (isSuccess)
        {
            await _uow.SaveChangesAsync(cancellationToken);
            await _uow.CommitAsync(cancellationToken);
        }
        else
        {
            await _uow.RollbackAsync(cancellationToken);
        }

        return result;
    }
}
