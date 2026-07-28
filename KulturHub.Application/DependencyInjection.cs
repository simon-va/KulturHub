using FluentValidation;
using KulturHub.Application.Features.Admin.Invitations.CreateInvitation;
using KulturHub.Application.Features.Platform.Auth.SignIn;
using KulturHub.Application.Features.Public.Auth.SignUp;
using Microsoft.Extensions.DependencyInjection;

namespace KulturHub.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddScoped<CreateInvitationHandler>();
        services.AddScoped<SignInHandler>();
        services.AddScoped<SignUpHandler>();

        return services;
    }
}