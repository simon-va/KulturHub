using FluentValidation;
using KulturHub.Application.Features.Events;
using KulturHub.Application.Features.Events.CreateEvent;
using KulturHub.Application.Features.Instagram;
using KulturHub.Application.Features.Instagram.RefreshToken;
using KulturHub.Application.Features.WeeklyPost;
using KulturHub.Application.Features.WeeklyPost.GenerateWeeklyPost;
using Microsoft.Extensions.DependencyInjection;

namespace KulturHub.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.AddScoped<IEventService, EventService>();
        services.AddScoped<IWeeklyPostService, WeeklyPostService>();
        services.AddScoped<IInstagramTokenService, InstagramTokenService>();

        return services;
    }
}
