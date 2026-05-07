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
    public EventStatus Status { get; private set; }
    public Guid? EventCategoryId { get; private set; }

    public void SetStatus(EventStatus status) => Status = status;

    public static Event Create(
        Guid organisationId,
        string title,
        DateTime startTime,
        DateTime endTime,
        string address,
        string description,
        Guid? eventCategoryId = null) => new()
    {
        Id = Guid.NewGuid(),
        OrganisationId = organisationId,
        Title = title,
        StartTime = startTime.ToUniversalTime(),
        EndTime = endTime.ToUniversalTime(),
        Address = address,
        Description = description,
        CreatedAt = DateTime.UtcNow,
        Status = EventStatus.Published,
        EventCategoryId = eventCategoryId,
    };
}
