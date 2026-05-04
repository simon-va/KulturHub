namespace KulturHub.Application.Features.Events.CreateEvent;

public record CreateEventInput(
    Guid OrganisationId,
    Guid UserId,
    string Title,
    DateTime StartTime,
    DateTime EndTime,
    string Address,
    string Description);
