using System.Security.Claims;
using KulturHub.Api.Extensions;
using KulturHub.Api.Requests;
using KulturHub.Api.Responses;
using KulturHub.Application.Features.Events.DeleteEvent;
using KulturHub.Application.Features.Events.GetConversation;
using KulturHub.Application.Features.Events.GetEvent;
using KulturHub.Application.Features.Events.GetEvents;
using KulturHub.Application.Features.Events.InitializeEvent;
using KulturHub.Application.Features.Events.SendMessage;
using KulturHub.Application.Features.Events.UpdateEventStatus;

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
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .WithName("Event_GetEvents")
        .WithTags("Event");

        app.MapGet("/organisations/{organisationId:guid}/events/{eventId:guid}", async (
            Guid organisationId,
            Guid eventId,
            ClaimsPrincipal user,
            IGetEventService getEventService) =>
        {
            var userId = user.GetUserId();
            var result = await getEventService.GetEventAsync(
                new GetEventInput(organisationId, eventId, userId));

            return result.Match(
                @event => Results.Ok(@event),
                errors => errors.ToResult());
        })
        .RequireAuthorization()
        .Produces<EventResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithName("Event_GetEventById")
        .WithTags("Event");

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
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .WithName("Event_InitializeEvent")
        .WithTags("Event");

        app.MapGet("/organisations/{organisationId:guid}/events/{eventId:guid}/conversation", async (
            Guid organisationId,
            Guid eventId,
            ClaimsPrincipal user,
            IGetConversationService getConversationService) =>
        {
            var userId = user.GetUserId();
            var result = await getConversationService.GetConversationAsync(
                new GetConversationInput(organisationId, eventId, userId));

            return result.Match(
                conversation => Results.Ok(conversation),
                errors => errors.ToResult());
        })
        .RequireAuthorization()
        .Produces<ConversationResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithName("Event_GetEventConversation")
        .WithTags("Event");

        app.MapPost("/organisations/{organisationId:guid}/events/{eventId:guid}/conversation/messages", async (
            Guid organisationId,
            Guid eventId,
            SendMessageRequest body,
            ClaimsPrincipal user,
            ISendMessageService sendMessageService,
            CancellationToken cancellationToken) =>
        {
            var userId = user.GetUserId();
            var result = await sendMessageService.SendMessageAsync(
                new SendMessageInput(organisationId, eventId, userId, body.Content),
                cancellationToken);

            return result.Match(
                message => Results.Ok(message),
                errors => errors.ToResult());
        })
        .RequireAuthorization()
        .Produces<SendMessageResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithName("Event_SendEventMessage")
        .WithTags("Event");

        app.MapDelete("/organisations/{organisationId:guid}/events/{eventId:guid}", async (
            Guid organisationId,
            Guid eventId,
            ClaimsPrincipal user,
            IDeleteEventService deleteEventService) =>
        {
            var userId = user.GetUserId();
            var result = await deleteEventService.DeleteEventAsync(
                new DeleteEventInput(organisationId, eventId, userId));

            return result.Match(
                _ => Results.NoContent(),
                errors => errors.ToResult());
        })
        .RequireAuthorization()
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithName("Event_DeleteEvent")
        .WithTags("Event");

        app.MapPatch("/organisations/{organisationId:guid}/events/{eventId:guid}/status", async (
            Guid organisationId,
            Guid eventId,
            UpdateEventStatusRequest body,
            ClaimsPrincipal user,
            IUpdateEventStatusService updateEventStatusService) =>
        {
            var userId = user.GetUserId();
            var result = await updateEventStatusService.UpdateEventStatusAsync(
                new UpdateEventStatusInput(organisationId, eventId, userId, body.Status));

            return result.Match(
                _ => Results.NoContent(),
                errors => errors.ToResult());
        })
        .RequireAuthorization()
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesValidationProblem()
        .WithName("Event_UpdateEventStatus")
        .WithTags("Event");
    }
}
