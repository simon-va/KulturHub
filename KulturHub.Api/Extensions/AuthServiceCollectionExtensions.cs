using System.IdentityModel.Tokens.Jwt;
using KulturHub.Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace KulturHub.Api.Extensions;

public static class AuthServiceCollectionExtensions
{
    static AuthServiceCollectionExtensions()
    {
        JwtSecurityTokenHandler.DefaultMapInboundClaims = false;
    }

    public static IServiceCollection AddKulturHubAuth(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var discoveryUrl = configuration["Supabase:DiscoveryUrl"]
            ?? throw new InvalidOperationException("Supabase:DiscoveryUrl is not configured.");

        services.Configure<SupabaseAuthOptions>(configuration.GetSection(SupabaseAuthOptions.SectionName));

        services.AddSingleton<Supabase.Client>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<SupabaseAuthOptions>>().Value;
            var supabaseOptions = new Supabase.SupabaseOptions
            {
                AutoRefreshToken = false,
            };
            return new Supabase.Client(options.Url, options.Key, supabaseOptions);
        });

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = discoveryUrl;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateAudience = true,
                    ValidAudience = "authenticated",
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });
        return services;
    }
}