using KulturHub.Domain.ChangeLogs;

namespace KulturHub.Application.Features.Platform.ChangeLogs.ListChangeLogs;

public sealed record ListChangeLogsCommand(
    Guid OrganisationId,
    int Skip,
    int Take,
    string? Search,
    ChangeLogCategory? Category);