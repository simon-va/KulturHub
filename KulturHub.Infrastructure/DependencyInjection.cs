using KulturHub.Application.Ports;
using KulturHub.Domain.Interfaces;
using KulturHub.Infrastructure.AI;
using KulturHub.Infrastructure.Auth;
using KulturHub.Infrastructure.ExternalApis;
using KulturHub.Infrastructure.ImageGeneration;
using KulturHub.Infrastructure.Instagram;
using KulturHub.Infrastructure.Persistence;
using KulturHub.Infrastructure.Persistence.Repositories;
using KulturHub.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenAI;

namespace KulturHub.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IDbConnectionFactory>(_ =>
            new DbConnectionFactory(configuration));

        services.AddScoped<UnitOfWork>();
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<UnitOfWork>());
        services.AddScoped<IConnectionProvider>(sp => sp.GetRequiredService<UnitOfWork>());

        services.AddScoped<IAuthRepository, AuthRepository>();
        services.AddScoped<IInvitationRepository, InvitationRepository>();
        services.AddScoped<IPostRepository, PostRepository>();
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IEventCategoryRepository, EventCategoryRepository>();
        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<IOrganisationRepository, OrganisationRepository>();
        services.AddScoped<IUserRepository, UserRepository>();

        var openAiApiKey = configuration["OpenAI:ApiKey"]
            ?? throw new InvalidOperationException("OpenAI:ApiKey is not configured.");
        services.AddSingleton(new OpenAIClient(openAiApiKey));
        services.AddScoped<IAiChatService, OpenAiChatService>();

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
