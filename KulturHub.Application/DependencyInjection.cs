using FluentValidation;
using KulturHub.Application.Features.Admin.Invitations.CreateInvitation;
using KulturHub.Application.Features.Platform.Auth.SignIn;
using KulturHub.Application.Features.Platform.Memberships.ChangeMembershipStatus;
using KulturHub.Application.Features.Platform.Memberships.DeleteMembership;
using KulturHub.Application.Features.Platform.Organisations.CreateOrganisation;
using KulturHub.Application.Features.Platform.Memberships.InviteMembership;
using KulturHub.Application.Features.Platform.Memberships.ListMemberships;
using KulturHub.Application.Features.Platform.Memberships.ListMyPendingMemberships;
using KulturHub.Application.Features.Platform.Organisations.ListMyOrganisations;
using KulturHub.Application.Features.Platform.Organisations.UpdateOrganisation;
using KulturHub.Application.Features.Public.Auth.SignUp;
using KulturHub.Application.Features.Public.Auth.ValidateInvitation;
using KulturHub.Application.Features.Platform.Users.GetCurrentUser;
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
        services.AddScoped<ValidateInvitationHandler>();
        services.AddScoped<CreateOrganisationHandler>();
        services.AddScoped<UpdateOrganisationHandler>();
        services.AddScoped<ListMyOrganisationsHandler>();
        services.AddScoped<ListMembershipsHandler>();
        services.AddScoped<InviteMembershipHandler>();
        services.AddScoped<DeleteMembershipHandler>();
        services.AddScoped<ChangeMembershipStatusHandler>();
        services.AddScoped<ListMyPendingMembershipsHandler>();
        services.AddScoped<GetCurrentUserHandler>();

        return services;
    }
}