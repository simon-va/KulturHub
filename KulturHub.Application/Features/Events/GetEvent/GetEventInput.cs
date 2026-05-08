namespace KulturHub.Application.Features.Events.GetEvent;

public record GetEventInput(Guid OrganisationId, Guid EventId, Guid UserId);
