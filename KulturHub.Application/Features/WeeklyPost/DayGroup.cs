namespace KulturHub.Application.Features.WeeklyPost;

public class DayGroup
{
    public DateOnly Date { get; init; }
    public string DayName { get; init; } = string.Empty;
    public List<ChaynsEvent> Events { get; init; } = [];
}
