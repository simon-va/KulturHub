using KulturHub.Application.Ports;
using KulturHub.Domain.Interfaces;
using KulturHub.Infrastructure.Auth;
using KulturHub.Infrastructure.ExternalApis;
using KulturHub.Infrastructure.ImageGeneration;
using KulturHub.Infrastructure.Instagram;
using KulturHub.Infrastructure.Persistence;
using KulturHub.Infrastructure.Persistence.Repositories;
using KulturHub.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KulturHub.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IDbConnectionFactory>(_ =>
            new DbConnectionFactory(configuration));

        services.AddScoped<IAuthRepository, AuthRepository>();
        services.AddScoped<IInvitationRepository, InvitationRepository>();
        services.AddScoped<IPostRepository, PostRepository>();
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IOrganisationRepository, OrganisationRepository>();

        services.AddSingleton(_ => new Supabase.Client(
            configuration["Supabase:Url"]
                ?? throw new InvalidOperationException("Supabase:Url is not configured."),
            configuration["Supabase:Key"]
                ?? throw new InvalidOperationException("Supabase:Key is not configured.")));

        services.AddScoped<IAuthProvider, SupabaseAuthProvider>();
        services.AddHttpClient<ISupabaseAdminClient, SupabaseAdminClient>();
        services.AddHttpClient<IChaynsApiClient, ChaynsApiClient>();

        services.AddSingleton<LayoutEngine>();
        services.AddSingleton<IImageGenerator, SkiaImageGenerator>();
        services.AddSingleton<IStorageService, SupabaseStorageService>();

        services.AddScoped<IInstagramTokenRepository, InstagramTokenRepository>();
        services.AddHttpClient<IInstagramPublisher, InstagramGraphApiPublisher>();
        services.AddHttpClient<IInstagramTokenRefresher, InstagramGraphApiPublisher>();

        return services;
    }
}
