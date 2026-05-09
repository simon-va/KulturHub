using KulturHub.Domain.Enums;

namespace KulturHub.Application.Features.Events.GetEvents;

public record EventResponse(
    Guid Id,
    Guid OrganisationId,
    string? Title,
    DateTime? StartTime,
    DateTime? EndTime,
    string? Address,
    string? Description,
    DateTime CreatedAt,
    EventStatus Status,
    string? ErrorMessage,
    int? EventCategoryId,
    Guid? ConversationId);
