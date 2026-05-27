namespace TruckManager.Application.Abstractions.Cqrs;

// [ADR-0038]   Cross-cutting concern that wraps every command / query handler invocation.
//
// Implementations:
//   - ValidationBehavior:  runs FluentValidators; short-circuits to Result.Failure on validation failure. Registered on BOTH dispatchers.
//   - UnitOfWorkBehavior:  transaction control (see [ADR-0039]). Registered on the COMMAND dispatcher only (Duh!)
//   - (deferred to Phase 7) LoggingBehavior:   structured request logging.
//
// Composition: dispatcher resolves IEnumerable<IPipelineBehavior<TRequest, TResult>> from DI and composes them in reverse-registration order around the handler call.
// First registered behavior runs outermost. Behaviors call `next()` to invoke the next layer (or the handler itself if they're innermost).
// A behavior that does NOT call `next()` short-circuits the pipeline -> return the desired TResult directly.
public interface IPipelineBehavior<TRequest, TResult>
{
    Task<TResult> HandleAsync(TRequest request, Func<Task<TResult>> next, CancellationToken cancellationToken);
}
