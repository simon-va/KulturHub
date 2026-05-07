namespace KulturHub.Application.Ports;

public interface IUnitOfWork : IAsyncDisposable
{
    Task BeginAsync();
    Task CommitAsync();
    Task RollbackAsync();
}
