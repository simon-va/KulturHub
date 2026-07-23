namespace KulturHub.Application.Features.ChangeLogs.ListOrganisationChangeLogs;

public sealed record ChangeLogListItem(
    Guid Id,
    Guid OrganisationId,
    Guid UserId,
    string UserFullName,
    string Message,
    IReadOnlyDictionary<string, object?> Data,
    DateTime CreatedAt);
