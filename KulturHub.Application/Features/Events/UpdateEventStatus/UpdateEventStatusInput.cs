using KulturHub.Domain.Enums;

namespace KulturHub.Application.Features.Events.UpdateEventStatus;

public record UpdateEventStatusInput(Guid OrganisationId, Guid EventId, Guid UserId, EventStatus Status);
