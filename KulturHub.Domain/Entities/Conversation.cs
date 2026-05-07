namespace KulturHub.Domain.Entities;

public class Conversation
{
    public Guid Id { get; private set; }
    public Guid OrganisationId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public static Conversation Create(Guid organisationId) => new()
    {
        Id = Guid.NewGuid(),
        OrganisationId = organisationId,
        CreatedAt = DateTime.UtcNow,
    };
}
