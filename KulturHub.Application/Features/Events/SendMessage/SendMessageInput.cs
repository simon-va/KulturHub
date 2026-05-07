namespace KulturHub.Application.Features.Events.SendMessage;

public record SendMessageInput(Guid OrganisationId, Guid EventId, Guid UserId, string Content);
