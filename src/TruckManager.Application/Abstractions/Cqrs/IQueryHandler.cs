namespace TruckManager.Application.Abstractions.Cqrs;

// [ADR-0038]   Handler for a read-side request. Queries are NOT wrapped in a transaction by the UnitOfWorkBehavior (registered on the command pipeline only (see[ADR-0039]));  Query handlers can't mutate state.
public interface IQueryHandler<TQuery, TResult> where TQuery : IQuery<TResult>
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken);
}
