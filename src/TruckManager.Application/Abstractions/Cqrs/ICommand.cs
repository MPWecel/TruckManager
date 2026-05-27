namespace TruckManager.Application.Abstractions.Cqrs;

// [ADR-0039]   Common marker shared by ICommand and ICommand<TResult>.
// UnitOfWorkBehavior<TCommand,TResult> is constrained to where TCommand implements IBaseCommand - the DI open-generic resolution skips it for IQuery<> types, so the transaction pipeline is absent from the query dispatcher without any per-type registration.
public interface IBaseCommand { }

// [ADR-0038]   Marker for command that does not return a payload. The matching handler returns Result (no wrapped value).
// Use this for delete / status-change / voidish mutations.
// For commands that produce a value (for instance, the new id from CreateTruck), use ICommand<TResult> instead.
public interface ICommand : IBaseCommand { }

// [ADR-0038]   Marker for command that returns a typed Result<T> payload. TResult is expected to be Result<T> (not raw T) so the failure shape stays consistent across the dispatcher.
public interface ICommand<TResult> : IBaseCommand { }
