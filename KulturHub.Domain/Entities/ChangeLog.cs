namespace KulturHub.Domain.Entities;

public class ChangeLog
{
    public Guid Id { get; private set; }
    public Guid OrganisationId { get; private set; }
    public Guid UserId { get; private set; }
    public string Message { get; private set; } = null!;
    public IReadOnlyDictionary<string, object?> Data { get; private set; } = new Dictionary<string, object?>();
    public DateTime CreatedAt { get; private set; }

    private ChangeLog() { }

    public static ChangeLog Create(
        Guid organisationId,
        Guid userId,
        string message,
        IReadOnlyDictionary<string, object?>? data = null)
    {
        if (organisationId == Guid.Empty)
            throw new ArgumentException("OrganisationId is required.", nameof(organisationId));
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId is required.", nameof(userId));

        var trimmedMessage = (message ?? string.Empty).Trim();
        if (trimmedMessage.Length == 0)
            throw new ArgumentException("Message is required.", nameof(message));

        return new ChangeLog
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            UserId = userId,
            Message = trimmedMessage,
            Data = data ?? new Dictionary<string, object?>(),
            CreatedAt = DateTime.UtcNow,
        };
    }

    public static ChangeLog Reconstitute(
        Guid id,
        Guid organisationId,
        Guid userId,
        string message,
        IReadOnlyDictionary<string, object?> data,
        DateTime createdAt) => new()
    {
        Id = id,
        OrganisationId = organisationId,
        UserId = userId,
        Message = message,
        Data = data,
        CreatedAt = createdAt,
    };
}
