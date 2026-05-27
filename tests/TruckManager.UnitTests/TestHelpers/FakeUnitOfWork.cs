using TruckManager.Application.Abstractions.Persistence;

namespace TruckManager.UnitTests.TestHelpers;

// Records every call so UnitOfWorkBehavior tests can assert on the exact call sequence.
internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int BeginTransactionCalls { get; private set; }
    public int SaveChangesCalls { get; private set; }
    public int CommitCalls { get; private set; }
    public int RollbackCalls { get; private set; }

    public Task BeginTransactionAsync(CancellationToken cancellationToken)
    {
        BeginTransactionCalls++;
        return Task.CompletedTask;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveChangesCalls++;
        return Task.FromResult(0);
    }

    public Task CommitAsync(CancellationToken cancellationToken)
    {
        CommitCalls++;
        return Task.CompletedTask;
    }

    public Task RollbackAsync(CancellationToken cancellationToken)
    {
        RollbackCalls++;
        return Task.CompletedTask;
    }
}
