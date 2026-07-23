using System.Data;
using System.Data.Common;
using KulturHub.Application.Ports;

namespace KulturHub.Infrastructure.Persistence.Internal;

internal static class UnitOfWorkTransactionExtensions
{
    internal static (DbConnection Connection, DbTransaction Transaction) Unwrap(
        this IUnitOfWorkTransaction tx)
    {
        if (tx is not UnitOfWork.NpgsqlUnitOfWorkTransaction concrete)
            throw new InvalidOperationException(
                $"Unknown IUnitOfWorkTransaction implementation: {tx?.GetType().FullName ?? "null"}");

        return (concrete.Connection, concrete.Transaction);
    }
}
