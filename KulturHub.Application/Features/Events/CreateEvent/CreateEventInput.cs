namespace KulturHub.Application.Features.Events.CreateEvent;

public record CreateEventInput(
    string Title,
    DateTime StartTime,
    DateTime EndTime,
    string Address,
    string Description);
