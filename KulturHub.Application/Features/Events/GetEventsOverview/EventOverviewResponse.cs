using KulturHub.Domain.Enums;

namespace KulturHub.Application.Features.Events.GetEventsOverview;

public record EventOverviewResponse(
    Guid Id,
    string? Title,
    DateTime? StartTime,
    EventStatus Status);
