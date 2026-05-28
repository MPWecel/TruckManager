using Microsoft.Extensions.DependencyInjection;

using TruckManager.Common.Results;
using TruckManager.Application.Abstractions.Cqrs;

namespace TruckManager.Application.Cqrs;

// [ADR-0038]   Command-side dispatcher implementation.
//
// For each SendAsync call:
//   1. Close the ICommandHandler<> generic over the runtime command type (and TResult).
//   2. Close the IPipelineBehavior<,> generic over the same types and resolve the registered behaviors as IEnumerable<>.
//   3. Compose the behaviors in reverse-registration order around the handler call — first registered runs outermost, last registered is closest to the handler.
//   4. Invoke the resulting Func<Task<TResult>>.
//
// 'dynamic' casts bridge "generic interface, runtime-known type parameters": language can't statically type this without reflection. Reflection is expensive. 'dynamic' is ugly. Ugly >>> expensive.
// Cost is negligible at this scale (one dispatch per HTTP request).
public sealed class CommandDispatcher : ICommandDispatcher
{
    private readonly IServiceProvider _services;

    public CommandDispatcher(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        
        _services = services;
    }

    public Task<Result> SendAsync(ICommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        Type commandType = command.GetType();
        Type handlerType = typeof(ICommandHandler<>).MakeGenericType(commandType);
        Type behaviorType = typeof(IPipelineBehavior<,>).MakeGenericType(commandType, typeof(Result));

        object handler = _services.GetRequiredService(handlerType);
        IEnumerable<object> behaviors = (IEnumerable<object>)(_services.GetServices(behaviorType));

        Func<Task<Result>> next = () => InvokeHandlerAsync<Result>(handler, command, cancellationToken);

        foreach (object behavior in behaviors.Reverse())
        {
            Func<Task<Result>> current = next;
            next = () => InvokeBehaviorAsync<Result>(behavior, command, current, cancellationToken);
        }

        return next();
    }

    public Task<TResult> SendAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        Type commandType = command.GetType();
        Type handlerType = typeof(ICommandHandler<,>).MakeGenericType(commandType, typeof(TResult));
        Type behaviorType = typeof(IPipelineBehavior<,>).MakeGenericType(commandType, typeof(TResult));

        object handler = _services.GetRequiredService(handlerType);
        IEnumerable<object> behaviors = (IEnumerable<object>)(_services.GetServices(behaviorType));

        Func<Task<TResult>> next = () => InvokeHandlerAsync<TResult>(handler, command, cancellationToken);

        foreach (object behavior in behaviors.Reverse())
        {
            Func<Task<TResult>> current = next;
            next = () => InvokeBehaviorAsync<TResult>(behavior, command, current, cancellationToken);
        }

        return next();
    }

    private static Task<T> InvokeHandlerAsync<T>(object handler, object command, CancellationToken cancellationToken)
        => (Task<T>)((dynamic)(handler)).HandleAsync((dynamic)(command), cancellationToken);

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
