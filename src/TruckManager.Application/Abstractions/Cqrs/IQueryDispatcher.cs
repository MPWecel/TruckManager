namespace TruckManager.Application.Abstractions.Cqrs;

// [ADR-0038]   Entry point for read query; Mirrors ICommandDispatcher but has only the typed overload 
// The query dispatcher's pipeline is registered separately from the command pipeline (see [ADR-0039]),UnitOfWorkBehavior runs ONLY on commands, so queries skip the transaction wrapper.
// ValidationBehavior still applies to queries.
public interface IQueryDispatcher
{
    Task<TResult> SendAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken);
}
