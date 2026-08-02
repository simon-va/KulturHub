using KulturHub.Domain.ChangeLogs;

namespace KulturHub.Application.Features.Platform.ChangeLogs.ListChangeLogs;

public sealed record ListChangeLogsRequest(
    int Skip,
    int Take,
    string? Search,
    ChangeLogCategory? Category);