using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace KulturHub.Infrastructure.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .AddUserSecrets("6e624591-7875-4f6e-bc3c-95870bfbcfa3")
            .Build();

        var connectionString = configuration.GetConnectionString("Default")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Default not found. Set it via user secrets in KulturHub.Api " +
                "or via the ConnectionStrings__Default environment variable.");

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString);

        return new AppDbContext(optionsBuilder.Options);
    }
}
