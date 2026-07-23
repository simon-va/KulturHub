using System.Data.Common;
using KulturHub.Application.Ports;
using Microsoft.Extensions.Logging;

namespace KulturHub.Infrastructure.Persistence;

public sealed class UnitOfWork(
    IDbConnectionFactory connectionFactory,
    ILogger<UnitOfWork> logger) : IUnitOfWork
{
    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var transaction = connection.BeginTransaction();
        logger.LogDebug("Began database transaction on connection {ConnectionId}.", connection.GetHashCode());
        return new NpgsqlUnitOfWorkTransaction(connection, transaction, logger);
    }

    internal sealed class NpgsqlUnitOfWorkTransaction(
        DbConnection connection,
        DbTransaction transaction,
        ILogger logger) : IUnitOfWorkTransaction
    {
        private bool _completed;

        internal DbConnection Connection => connection;
        internal DbTransaction Transaction => transaction;

        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            if (_completed) return;

            await transaction.CommitAsync(cancellationToken);
            _completed = true;
            logger.LogDebug("Committed database transaction on connection {ConnectionId}.", connection.GetHashCode());
        }

        public async Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            if (_completed) return;

            try
            {
                await transaction.RollbackAsync(cancellationToken);
                logger.LogDebug("Rolled back database transaction on connection {ConnectionId}.", connection.GetHashCode());
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Rollback of database transaction on connection {ConnectionId} failed.",
                    connection.GetHashCode());
            }
            finally
            {
                _completed = true;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (!_completed)
                await RollbackAsync();

            await transaction.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
