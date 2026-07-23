using FluentValidation;
using KulturHub.Application.Features.Auth.DeleteAccount;
using KulturHub.Application.Features.Auth.SignIn;
using KulturHub.Application.Features.Auth.SignUp;
using KulturHub.Application.Features.Auth.ValidateInvitation;
using KulturHub.Application.Features.ChangeLogs.ListOrganisationChangeLogs;
using KulturHub.Application.Features.Invitations.CreateInvitation;
using KulturHub.Application.Features.Invitations.DeleteInvitation;
using KulturHub.Application.Features.Invitations.ListInvitations;
using KulturHub.Application.Features.Memberships.DeleteOrganisationMembership;
using KulturHub.Application.Features.Memberships.InviteMember;
using KulturHub.Application.Features.Memberships.ListMyPendingMemberships;
using KulturHub.Application.Features.Memberships.ListOrganisationMemberships;
using KulturHub.Application.Features.Memberships.RespondToMembership;
using KulturHub.Application.Features.Organisations.CreateOrganisation;
using KulturHub.Application.Features.Organisations.ListUserOrganisations;
using KulturHub.Application.Features.Organisations.UpdateOrganisation;
using Microsoft.Extensions.DependencyInjection;

namespace KulturHub.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.AddScoped<SignUpHandler>();
        services.AddScoped<ValidateInvitationHandler>();
        services.AddScoped<SignInHandler>();
        services.AddScoped<DeleteAccountHandler>();
        services.AddScoped<CreateInvitationHandler>();
        services.AddScoped<ListInvitationsHandler>();
        services.AddScoped<DeleteInvitationHandler>();
        services.AddScoped<CreateOrganisationHandler>();
        services.AddScoped<ListUserOrganisationsHandler>();
        services.AddScoped<UpdateOrganisationHandler>();
        services.AddScoped<ListOrganisationMembershipsHandler>();
        services.AddScoped<DeleteOrganisationMembershipHandler>();
        services.AddScoped<InviteMemberHandler>();
        services.AddScoped<ListMyPendingMembershipsHandler>();
        services.AddScoped<RespondToMembershipHandler>();
        services.AddScoped<ListOrganisationChangeLogsHandler>();

        return services;
    }
}
