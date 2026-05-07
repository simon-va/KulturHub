using KulturHub.Domain.Enums;

namespace KulturHub.Domain.Entities;

public class Event
{
    public Guid Id { get; private set; }
    public Guid OrganisationId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public DateTime? StartTime { get; private set; }
    public DateTime? EndTime { get; private set; }
    public string Address { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public EventStatus Status { get; private set; }
    public string? ErrorMessage { get; private set; }
    public Guid? EventCategoryId { get; private set; }
    public Guid? ConversationId { get; private set; }

    public void SetStatus(EventStatus status) => Status = status;

    public void UpdateDraft(string? title, string? address, string? description,
                            DateTime? startTime, DateTime? endTime, EventStatus status)
    {
        if (title is not null) Title = title;
        if (address is not null) Address = address;
        if (description is not null) Description = description;
        if (startTime is not null) StartTime = startTime;
        if (endTime is not null) EndTime = endTime;
        Status = status;
    }

    public void SetFailed(string errorMessage)
    {
        Status = EventStatus.Failed;
        ErrorMessage = errorMessage;
    }

    public static Event CreateDraft(Guid organisationId, Guid conversationId) => new()
    {
        Id = Guid.NewGuid(),
        OrganisationId = organisationId,
        CreatedAt = DateTime.UtcNow,
        Status = EventStatus.Draft,
        ConversationId = conversationId,
    };

    public static Event Reconstitute(
        Guid id, Guid organisationId, string title,
        DateTime? startTime, DateTime? endTime,
        string address, string description, DateTime createdAt,
        EventStatus status, string? errorMessage,
        Guid? eventCategoryId, Guid? conversationId) => new()
    {
        Id = id,
        OrganisationId = organisationId,
        Title = title,
        StartTime = startTime,
        EndTime = endTime,
        Address = address,
        Description = description,
        CreatedAt = createdAt,
        Status = status,
        ErrorMessage = errorMessage,
        EventCategoryId = eventCategoryId,
        ConversationId = conversationId,
    };
}
