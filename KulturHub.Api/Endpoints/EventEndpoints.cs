using System.Security.Claims;
using KulturHub.Api.Extensions;
using KulturHub.Api.Responses;
using KulturHub.Application.Features.Events.GetEvents;
using KulturHub.Application.Features.Events.InitializeEvent;

namespace KulturHub.Api.Endpoints;

public static class EventEndpoints
{
    public static void MapEventEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/organisations/{organisationId:guid}/events", async (
            Guid organisationId,
            ClaimsPrincipal user,
            IGetEventsService getEventsService) =>
        {
            var userId = user.GetUserId();
            var result = await getEventsService.GetEventsAsync(
                new GetEventsInput(organisationId, userId));

            return result.Match(
                events => Results.Ok(events),
                errors => errors.ToResult());
        })
        .RequireAuthorization()
        .Produces<IEnumerable<EventResponse>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        app.MapPost("/organisations/{organisationId:guid}/events/initialize", async (
            Guid organisationId,
            ClaimsPrincipal user,
            IInitializeEventService initializeEventService) =>
        {
            var userId = user.GetUserId();
            var result = await initializeEventService.InitializeEventAsync(
                new InitializeEventInput(organisationId, userId));

            return result.Match(
                id => Results.Created($"/events/{id}", new CreatedResponse(id)),
                errors => errors.ToResult());
        })
        .RequireAuthorization()
        .Produces<CreatedResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);
    }
}
