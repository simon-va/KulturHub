using KulturHub.Domain.ChangeLogs;

namespace KulturHub.Application.Features.Platform.ChangeLogs.ListChangeLogs;

public sealed record ChangeLogResponse(
    Guid Id,
    Guid CreatedBy,
    string CreatedByFullName,
    string Message,
    ChangeLogCategory Category,
    IReadOnlyDictionary<string, string?> Data,
    DateTime CreatedAt);