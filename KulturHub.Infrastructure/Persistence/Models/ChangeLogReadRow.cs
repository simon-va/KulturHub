namespace KulturHub.Infrastructure.Persistence.Models;

public sealed class ChangeLogReadRow
{
    public Guid Id { get; init; }
    public Guid OrganisationId { get; init; }
    public Guid UserId { get; init; }
    public string Message { get; init; } = default!;
    public string Data { get; init; } = default!;
    public DateTime CreatedAt { get; init; }
    public string UserFirstName { get; init; } = default!;
    public string UserLastName { get; init; } = default!;
}
