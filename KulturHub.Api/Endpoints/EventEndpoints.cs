using System.Security.Claims;
using KulturHub.Api.Extensions;
using KulturHub.Api.Filters;
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
            IGetEventsService getEventsService) =>
        {
            var result = await getEventsService.GetEventsAsync(
                new GetEventsInput(organisationId));

            return result.Match(
                events => Results.Ok(events),
                errors => errors.ToResult());
        })
        .RequireAuthorization()
        .Produces<IEnumerable<EventResponse>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .RequireOrganisationMembership()
        .WithName("Event_GetEvents")
        .WithTags("Event");

        app.MapGet("/organisations/{organisationId:guid}/events/{eventId:guid}", async (
            Guid organisationId,
            Guid eventId,
            IGetEventService getEventService) =>
        {
            var result = await getEventService.GetEventAsync(
                new GetEventInput(organisationId, eventId));

            return result.Match(
                @event => Results.Ok(@event),
                errors => errors.ToResult());
        })
        .RequireAuthorization()
        .Produces<EventResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .RequireOrganisationMembership()
        .WithName("Event_GetEventById")
        .WithTags("Event");

        app.MapPost("/organisations/{organisationId:guid}/events/initialize", async (
            Guid organisationId,
            IInitializeEventService initializeEventService) =>
        {
            var result = await initializeEventService.InitializeEventAsync(
                new InitializeEventInput(organisationId));

            return result.Match(
                id => Results.Created($"/events/{id}", new CreatedResponse(id)),
                errors => errors.ToResult());
        })
        .RequireAuthorization()
        .Produces<CreatedResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .WithUnitOfWork()
        .RequireOrganisationMembership()
        .WithName("Event_InitializeEvent")
        .WithTags("Event");

        app.MapGet("/organisations/{organisationId:guid}/events/{eventId:guid}/conversation", async (
            Guid organisationId,
            Guid eventId,
            IGetConversationService getConversationService) =>
        {
            var result = await getConversationService.GetConversationAsync(
                new GetConversationInput(organisationId, eventId));

            return result.Match(
                conversation => Results.Ok(conversation),
                errors => errors.ToResult());
        })
        .RequireAuthorization()
        .Produces<ConversationResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .RequireOrganisationMembership()
        .WithName("Event_GetEventConversation")
        .WithTags("Event");

        app.MapPost("/organisations/{organisationId:guid}/events/{eventId:guid}/conversation/messages", async (
            Guid organisationId,
            Guid eventId,
            SendMessageRequest body,
            ISendMessageService sendMessageService,
            CancellationToken cancellationToken) =>
        {
            var result = await sendMessageService.SendMessageAsync(
                new SendMessageInput(organisationId, eventId, body.Content),
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
        .WithUnitOfWork()
        .RequireOrganisationMembership()
        .WithName("Event_SendEventMessage")
        .WithTags("Event");

        app.MapDelete("/organisations/{organisationId:guid}/events/{eventId:guid}", async (
            Guid organisationId,
            Guid eventId,
            IDeleteEventService deleteEventService) =>
        {
            var result = await deleteEventService.DeleteEventAsync(
                new DeleteEventInput(organisationId, eventId));

            return result.Match(
                _ => Results.NoContent(),
                errors => errors.ToResult());
        })
        .RequireAuthorization()
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithUnitOfWork()
        .RequireOrganisationMembership()
        .WithName("Event_DeleteEvent")
        .WithTags("Event");

        app.MapPatch("/organisations/{organisationId:guid}/events/{eventId:guid}/status", async (
            Guid organisationId,
            Guid eventId,
            UpdateEventStatusRequest body,
            IUpdateEventStatusService updateEventStatusService) =>
        {
            var result = await updateEventStatusService.UpdateEventStatusAsync(
                new UpdateEventStatusInput(organisationId, eventId, body.Status));

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
        .WithUnitOfWork()
        .RequireOrganisationMembership()
        .WithName("Event_UpdateEventStatus")
        .WithTags("Event");
    }
}
