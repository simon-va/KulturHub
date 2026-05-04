using KulturHub.Domain.Enums;

namespace KulturHub.Domain.Entities;

public class Event
{
    public Guid Id { get; private set; }
    public Guid OrganisationId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public DateTime StartTime { get; private set; }
    public DateTime EndTime { get; private set; }
    public string Address { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public int? ChaynsEventId { get; private set; }
    public EventStatus Status { get; private set; }
    public string? ErrorMessage { get; private set; }

    public void SetChaynsEventId(int id) => ChaynsEventId = id;
    public void SetStatus(EventStatus status) => Status = status;
    public void SetErrorMessage(string message) => ErrorMessage = message;

    public static Event Create(
        Guid organisationId,
        string title,
        DateTime startTime,
        DateTime endTime,
        string address,
        string description) => new()
    {
        Id = Guid.NewGuid(),
        OrganisationId = organisationId,
        Title = title,
        StartTime = startTime.ToUniversalTime(),
        EndTime = endTime.ToUniversalTime(),
        Address = address,
        Description = description,
        CreatedAt = DateTime.UtcNow,
        Status = EventStatus.Draft,
    };
}
