using Microsoft.EntityFrameworkCore.Storage;

using TruckManager.Application.Abstractions.Persistence;

namespace TruckManager.Infrastructure.Persistence;

// [ADR-0039]   EF Core-backed Unit of Work. Wraps the scoped ApplicationDbContext so the active DB transaction is shared with every handler that injects IApplicationDbContext in the same request scope (same-instance proxy — ADR-0034 pattern)
// Registered as scoped in DependencyInjection.cs - one instance per HTTP request.
public sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _dbContext;
    private IDbContextTransaction? _transaction;

    public EfUnitOfWork(ApplicationDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken) => _transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => _dbContext.SaveChangesAsync(cancellationToken);

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        if (_transaction is null)
            throw new InvalidOperationException("No active transaction to commit.");

        await _transaction.CommitAsync(cancellationToken);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async Task RollbackAsync(CancellationToken cancellationToken)
    {
        if (_transaction is null) return; // already rolled back or never started — no-op

        await _transaction.RollbackAsync(cancellationToken);
        await _transaction.DisposeAsync();
        _transaction = null;
    }
}
