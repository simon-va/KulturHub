using FluentValidation;
using KulturHub.Application.Features.Auth;
using KulturHub.Application.Features.Auth.SignUp;
using KulturHub.Application.Features.Events.DeleteEvent;
using KulturHub.Application.Features.Events.GetConversation;
using KulturHub.Application.Features.Events.GetEvent;
using KulturHub.Application.Features.Events.GetEventCategories;
using KulturHub.Application.Features.Events.GetEvents;
using KulturHub.Application.Features.Events.GetEventsOverview;
using KulturHub.Application.Features.Events.InitializeEvent;
using KulturHub.Application.Features.Events.SendMessage;
using KulturHub.Application.Features.Events.UpdateEventStatus;
using KulturHub.Application.Features.Instagram;
using KulturHub.Application.Features.Instagram.RefreshToken;
using KulturHub.Application.Features.Invitations.CreateInvitation;
using KulturHub.Application.Features.Organisations;
using KulturHub.Application.Features.Users;
using KulturHub.Application.Features.WeeklyPost;
using KulturHub.Application.Features.WeeklyPost.GenerateWeeklyPost;
using Microsoft.Extensions.DependencyInjection;

namespace KulturHub.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IGetEventsService, GetEventsService>();
        services.AddScoped<IGetEventsOverviewService, GetEventsOverviewService>();
        services.AddScoped<IGetEventService, GetEventService>();
        services.AddScoped<IGetEventCategoriesService, GetEventCategoriesService>();
        services.AddScoped<IInitializeEventService, InitializeEventService>();
        services.AddScoped<IGetConversationService, GetConversationService>();
        services.AddScoped<ISendMessageService, SendMessageService>();
        services.AddScoped<IWeeklyPostService, WeeklyPostService>();
        services.AddScoped<IInstagramTokenService, InstagramTokenService>();
        services.AddScoped<IOrganisationService, OrganisationService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ICreateInvitationService, CreateInvitationService>();
        services.AddScoped<IUpdateEventStatusService, UpdateEventStatusService>();
        services.AddScoped<IDeleteEventService, DeleteEventService>();

        return services;
    }
}
