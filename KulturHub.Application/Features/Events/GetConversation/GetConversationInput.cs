namespace KulturHub.Application.Features.Events.GetConversation;

public record GetConversationInput(Guid OrganisationId, Guid EventId, Guid UserId);
