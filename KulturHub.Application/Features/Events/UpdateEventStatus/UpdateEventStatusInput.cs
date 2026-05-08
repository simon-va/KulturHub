using KulturHub.Domain.Enums;

namespace KulturHub.Application.Features.Events.UpdateEventStatus;

public record UpdateEventStatusInput(Guid OrganisationId, Guid EventId, EventStatus Status);
