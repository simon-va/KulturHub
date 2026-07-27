using KulturHub.Infrastructure.Auth;
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
        services.Configure<SupabaseAuthOptions>(configuration.GetSection(SupabaseAuthOptions.SectionName));
        services.AddSingleton<IConfigureOptions<SupabaseAuthOptions>, ConfigureSupabaseAuthOptions>();

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<SupabaseAuthOptions>>().Value;
            return new Supabase.Client(options.Url, options.Key);
        });

        return services;
    }

    private sealed class ConfigureSupabaseAuthOptions(IConfiguration configuration) : IConfigureOptions<SupabaseAuthOptions>
    {
        public void Configure(SupabaseAuthOptions options)
        {
            var section = configuration.GetSection(SupabaseAuthOptions.SectionName);
            options.Url = section["Url"] ?? string.Empty;
            options.Key = section["Key"] ?? string.Empty;
            options.DiscoveryUrl = section["DiscoveryUrl"] ?? string.Empty;
        }
    }
}
