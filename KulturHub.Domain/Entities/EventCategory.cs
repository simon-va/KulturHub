namespace KulturHub.Domain.Entities;

public class EventCategory
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Color { get; private set; } = string.Empty;
}
