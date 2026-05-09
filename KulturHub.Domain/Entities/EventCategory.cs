namespace KulturHub.Domain.Entities;

public class EventCategory
{
    public int Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Color { get; private set; } = string.Empty;

    public static EventCategory Reconstitute(int id, string name, string color) => new()
    {
        Id = id,
        Name = name,
        Color = color,
    };
}
