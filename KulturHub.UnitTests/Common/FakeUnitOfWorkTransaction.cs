using KulturHub.Application.Ports;

namespace KulturHub.UnitTests.Common;

internal sealed class FakeUnitOfWorkTransaction : IUnitOfWorkTransaction
{
    public int CommitCount { get; private set; }
    public int RollbackCount { get; private set; }
    public int DisposeCount { get; private set; }

    public bool ThrowOnCommit { get; set; }

    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (ThrowOnCommit)
            throw new InvalidOperationException("commit failed");

        CommitCount++;
        return Task.CompletedTask;
    }

    public Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        RollbackCount++;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        DisposeCount++;
        if (CommitCount == 0 && RollbackCount == 0)
            RollbackCount++;

        return ValueTask.CompletedTask;
    }
}
