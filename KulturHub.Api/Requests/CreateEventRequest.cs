namespace KulturHub.Api.Requests;

public record CreateEventRequest(
    string Title,
    DateTime StartTime,
    DateTime EndTime,
    string Address,
    string Description);
