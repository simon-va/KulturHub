using System.Data.Common;

namespace KulturHub.Infrastructure.Persistence;

public interface IConnectionProvider
{
    DbConnection Connection { get; }
    DbTransaction? Transaction { get; }
}
