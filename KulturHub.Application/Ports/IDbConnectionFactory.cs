using System.Data.Common;

namespace KulturHub.Application.Ports;

public interface IDbConnectionFactory
{
    DbConnection CreateConnection();
}
