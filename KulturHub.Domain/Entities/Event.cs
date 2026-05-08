using KulturHub.Domain.Enums;
using KulturHub.Domain.Exceptions;

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

    public void UpdateDetails(string title, string address, string description,
                              DateTime startTime, DateTime endTime)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("Title is required.");
        if (endTime <= startTime)
            throw new DomainException("End time must be after start time.");
        if (Status == EventStatus.Published)
            throw new DomainException("Cannot modify a published event.");

        Title = title;
        Address = address;
        Description = description;
        StartTime = startTime;
        EndTime = endTime;
        Status = EventStatus.ReadyToPublish;
    }

    public void Publish()
    {
        if (Status != EventStatus.ReadyToPublish)
            throw new DomainException("Only ready events can be published.");

        Status = EventStatus.Published;
    }

    public void MarkReadyToPublish()
    {
        if (Status != EventStatus.Draft)
            throw new DomainException("Only draft events can be marked as ready.");

        Status = EventStatus.ReadyToPublish;
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
