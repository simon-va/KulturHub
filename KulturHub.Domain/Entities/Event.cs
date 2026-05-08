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
    public int Version { get; private set; }

    public void UpdateDetails(string? title = null, string? address = null, string? description = null,
                              DateTime? startTime = null, DateTime? endTime = null,
                              EventStatus? newStatus = null)
    {
        if (Status == EventStatus.Published)
            throw new DomainException("Cannot modify a published event.");

        if (title is not null)
        {
            if (string.IsNullOrWhiteSpace(title))
                throw new DomainException("Title is required.");
            Title = title;
        }

        if (address is not null)
        {
            if (string.IsNullOrWhiteSpace(address))
                throw new DomainException("Address is required.");
            Address = address;
        }

        if (description is not null)
        {
            if (string.IsNullOrWhiteSpace(description))
                throw new DomainException("Description is required.");
            Description = description;
        }

        if (startTime.HasValue)
        {
            if (startTime.Value <= DateTime.UtcNow)
                throw new DomainException("Start time must be in the future.");
            StartTime = startTime.Value;
        }

        if (endTime.HasValue)
        {
            var effectiveStart = StartTime ?? DateTime.MinValue;
            if (endTime.Value <= effectiveStart)
                throw new DomainException("End time must be after start time.");
            EndTime = endTime.Value;
        }

        if (newStatus.HasValue)
            Status = newStatus.Value;
    }

    public void Publish()
    {
        if (Status != EventStatus.ReadyToPublish)
            throw new DomainException("Only ready events can be published.");

        Status = EventStatus.Published;
    }

    public void RevertToDraft()
    {
        if (Status != EventStatus.Published)
            throw new DomainException("Only published events can be reverted to draft.");

        Status = EventStatus.Draft;
    }

    public void SetFailed(string errorMessage)
    {
        Status = EventStatus.Failed;
        ErrorMessage = errorMessage;
    }

    public void IncrementVersion()
    {
        Version++;
    }

    public static Event CreateDraft(Guid organisationId, Guid conversationId) => new()
    {
        Id = Guid.NewGuid(),
        OrganisationId = organisationId,
        CreatedAt = DateTime.UtcNow,
        Status = EventStatus.Draft,
        ConversationId = conversationId,
        Version = 0,
    };

    public static Event Reconstitute(
        Guid id, Guid organisationId, string title,
        DateTime? startTime, DateTime? endTime,
        string address, string description, DateTime createdAt,
        EventStatus status, string? errorMessage,
        Guid? eventCategoryId, Guid? conversationId, int version) => new()
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
        Version = version,
    };
}
