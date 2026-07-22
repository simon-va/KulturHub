using KulturHub.Application.Ports;
using KulturHub.Infrastructure.Auth;
using KulturHub.Infrastructure.Persistence;
using KulturHub.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KulturHub.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<SupabaseOptions>(configuration.GetSection(SupabaseOptions.SectionName));
        services.AddSingleton<IConfigureOptions<SupabaseOptions>, ConfigureSupabaseOptions>();

        services.AddSingleton<IDbConnectionFactory>(_ =>
            new DbConnectionFactory(configuration));

        services.AddScoped<IAuthRepository, AuthRepository>();

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<SupabaseOptions>>().Value;
            return new Supabase.Client(options.Url, options.Key);
        });

        services.AddScoped<IAuthProvider, SupabaseAuthProvider>();
        services.AddHttpClient<ISupabaseAdminClient, SupabaseAdminClient>();

        return services;
    }

    private sealed class ConfigureSupabaseOptions(IConfiguration configuration) : IConfigureOptions<SupabaseOptions>
    {
        public void Configure(SupabaseOptions options)
        {
            var section = configuration.GetSection(SupabaseOptions.SectionName);
            options.Url = section["Url"] ?? string.Empty;
            options.Key = section["Key"] ?? string.Empty;
            options.DiscoveryUrl = section["DiscoveryUrl"] ?? string.Empty;
        }
    }
}
