using System.Data.Common;
using KulturHub.Application.Ports;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace KulturHub.Infrastructure.Persistence;

public class DbConnectionFactory(IConfiguration configuration) : IDbConnectionFactory
{
    private readonly string _connectionString = configuration.GetConnectionString("Default")
        ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");

    static DbConnectionFactory()
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", false);
    }

    public DbConnection CreateConnection() => new NpgsqlConnection(_connectionString);
}
