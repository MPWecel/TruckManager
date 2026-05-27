namespace TruckManager.Application.Abstractions.Cqrs;

// [ADR-0038]   Marker interface for a read request. Queries always return a typed result (usually Result<TDto> or Result<PagedListDto<TDto>>);
// No resultless queries foreseen in current scope (I mean WTF).
public interface IQuery<TResult> { }
