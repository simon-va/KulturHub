namespace KulturHub.Application.Features.Events.DeleteEvent;

public record DeleteEventInput(Guid OrganisationId, Guid EventId, Guid UserId);
