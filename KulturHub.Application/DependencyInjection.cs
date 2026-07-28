using FluentValidation;
using KulturHub.Application.Features.Admin.Invitations.CreateInvitation;
using Microsoft.Extensions.DependencyInjection;

namespace KulturHub.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddScoped<CreateInvitationHandler>();

        return services;
    }
}
