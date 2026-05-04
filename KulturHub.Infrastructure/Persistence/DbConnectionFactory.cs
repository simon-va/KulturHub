using System.Data.Common;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace KulturHub.Infrastructure.Persistence;

public class DbConnectionFactory(IConfiguration configuration) : IDbConnectionFactory
{
    private readonly string _connectionString = configuration.GetConnectionString("Default")
        ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");

    public DbConnection CreateConnection() => new NpgsqlConnection(_connectionString);
}
