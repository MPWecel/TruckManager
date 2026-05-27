using Microsoft.Extensions.DependencyInjection;

using TruckManager.Application.Abstractions.Cqrs;

namespace TruckManager.Application.Cqrs;

// [ADR-0038]   Query-side dispatcher implementation. Mirrors CommandDispatcher but without the resultless overload (Queries can't be resultless).
// Query pipeline shares ValidationBehavior with the Command pipeline but does NOT include UnitOfWorkBehavior.
// Queries are read-only and shouldn't be part of a transaction [ADR-0039].
public sealed class QueryDispatcher : IQueryDispatcher
{
    private readonly IServiceProvider _services;

    public QueryDispatcher(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
    }

    public Task<TResult> SendAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        Type queryType = query.GetType();
        Type handlerType = typeof(IQueryHandler<,>).MakeGenericType(queryType, typeof(TResult));
        Type behaviorType = typeof(IPipelineBehavior<,>).MakeGenericType(queryType, typeof(TResult));

        object handler = _services.GetRequiredService(handlerType);
        IEnumerable<object> behaviors = (IEnumerable<object>)(_services.GetServices(behaviorType));

        Func<Task<TResult>> next = () => InvokeHandlerAsync<TResult>(handler, query, cancellationToken);

        foreach (object behavior in behaviors.Reverse())
        {
            Func<Task<TResult>> current = next;
            next = () => InvokeBehaviorAsync<TResult>(behavior, query, current, cancellationToken);
        }

        return next();
    }

    private static Task<T> InvokeHandlerAsync<T>(
                                                    object handler, 
                                                    object query, 
                                                    CancellationToken cancellationToken
                                                )
        => (Task<T>)((dynamic)(handler)).HandleAsync((dynamic)(query), cancellationToken);

    private static Task<T> InvokeBehaviorAsync<T>(
                                                     object behavior, 
                                                     object request, 
                                                     Func<Task<T>> next, 
                                                     CancellationToken cancellationToken
                                                 )
        => (Task<T>)((dynamic)(behavior)).HandleAsync(
                                                         (dynamic)(request), 
                                                         next, 
                                                         cancellationToken
                                                     );
}
