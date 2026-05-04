namespace KulturHub.Domain.Entities;

public class Organisation
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;

    public static Organisation Create(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
    };
}
