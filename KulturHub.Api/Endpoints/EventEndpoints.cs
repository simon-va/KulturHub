using System.Security.Claims;
using KulturHub.Api.Extensions;
using KulturHub.Api.Requests;
using KulturHub.Api.Responses;
using KulturHub.Application.Features.Events;
using KulturHub.Application.Features.Events.CreateEvent;

namespace KulturHub.Api.Endpoints;

public static class EventEndpoints
{
    public static void MapEventEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/organisations/{organisationId:guid}/events", async (
            Guid organisationId,
            CreateEventRequest req,
            ClaimsPrincipal user,
            IEventService eventService) =>
        {
            var userId = user.GetUserId();
            var result = await eventService.CreateEventAsync(
                new CreateEventInput(organisationId, userId, req.Title, req.StartTime, req.EndTime, req.Address, req.Description));

            return result.Match(
                id => Results.Created($"/events/{id}", new CreatedResponse(id)),
                errors => errors.ToResult());
        })
        .RequireAuthorization()
        .Produces<CreatedResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
