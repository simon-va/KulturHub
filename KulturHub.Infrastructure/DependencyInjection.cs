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
        services.Configure<SupabaseAuthOptions>(configuration.GetSection(SupabaseAuthOptions.SectionName));
        services.AddSingleton<IConfigureOptions<SupabaseAuthOptions>, ConfigureSupabaseAuthOptions>();

        services.AddSingleton<IDbConnectionFactory>(_ =>
            new DbConnectionFactory(configuration));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IInvitationRepository, InvitationRepository>();
        services.AddScoped<IOrganisationRepository, OrganisationRepository>();
        services.AddScoped<IMembershipRepository, MembershipRepository>();
        services.AddScoped<IChangeLogRepository, ChangeLogRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<SupabaseAuthOptions>>().Value;
            return new Supabase.Client(options.Url, options.Key);
        });

        services.AddScoped<IAuthProvider, SupabaseAuthProvider>();
        services.AddHttpClient<IUserAdminClient, SupabaseUserAdminClient>();

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
