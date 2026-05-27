namespace TruckManager.Application.Abstractions.Cqrs;

// [ADR-0038]   Marker for command that does not return a payload. The matching handler returns Result (success/failure-with-errors, no wrapped value).
// Use this for delete / status-change / voidish mutations.
// For commands that produce a value (for instance, the new id from CreateTruck), use ICommand<TResult> instead.
public interface ICommand
{
}

// [ADR-0038]   Marker for command that returns a typed Result<T> payload. TResult is expected to be Result<T> (not raw T) so the failure shape stays consistent across the dispatcher.
public interface ICommand<TResult>
{
}
