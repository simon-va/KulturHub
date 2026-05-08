using System.Data.Common;

namespace KulturHub.Infrastructure.Persistence;

/// <summary>
/// Infrastructure-only contract for providing the current database connection and transaction.
/// Repositories in the Infrastructure layer use this to enlist operations in the active unit of work.
/// Application code should depend on <see cref="IUnitOfWork"/> instead.
/// </summary>
public interface IConnectionProvider
{
    DbConnection Connection { get; }
    DbTransaction? Transaction { get; }
}
