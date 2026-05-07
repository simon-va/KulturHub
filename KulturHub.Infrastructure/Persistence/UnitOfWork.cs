using System.Data.Common;
using KulturHub.Application.Ports;

namespace KulturHub.Infrastructure.Persistence;

public class UnitOfWork(IDbConnectionFactory connectionFactory) : IUnitOfWork, IConnectionProvider
{
    private DbConnection? _connection;
    private DbTransaction? _transaction;

    public DbConnection Connection => _connection ?? throw new InvalidOperationException("UnitOfWork has not been started. Call BeginAsync first.");
    public DbTransaction? Transaction => _transaction;

    public async Task BeginAsync()
    {
        _connection = connectionFactory.CreateConnection();
        await _connection.OpenAsync();
        _transaction = await _connection.BeginTransactionAsync();
    }

    public async Task CommitAsync()
    {
        if (_transaction is null) throw new InvalidOperationException("No active transaction.");
        await _transaction.CommitAsync();
    }

    public async Task RollbackAsync()
    {
        if (_transaction is null) throw new InvalidOperationException("No active transaction.");
        await _transaction.RollbackAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null) await _transaction.DisposeAsync();
        if (_connection is not null) await _connection.DisposeAsync();
    }
}
