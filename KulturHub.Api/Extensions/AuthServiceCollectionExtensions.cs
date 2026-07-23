using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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
