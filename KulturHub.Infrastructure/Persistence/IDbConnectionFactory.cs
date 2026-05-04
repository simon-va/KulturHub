using System.Data.Common;

namespace KulturHub.Infrastructure.Persistence;

public interface IDbConnectionFactory
{
    DbConnection CreateConnection();
}
