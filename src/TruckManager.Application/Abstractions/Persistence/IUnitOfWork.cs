namespace TruckManager.Application.Abstractions.Persistence;

// [ADR-0039]   Unit-of-work abstraction. UnitOfWorkBehavior is the sole caller of these methods;
// handlers never commit or roll back directly - they just mutate aggregates and return Result.
// Registered as scoped in Infrastructure - one instance per HTTP request, wrapping the same ApplicationDbContext that handlers access via IApplicationDbContext.
public interface IUnitOfWork
{
    Task BeginTransactionAsync(CancellationToken cancellationToken);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    Task CommitAsync(CancellationToken cancellationToken);
    Task RollbackAsync(CancellationToken cancellationToken);
}
