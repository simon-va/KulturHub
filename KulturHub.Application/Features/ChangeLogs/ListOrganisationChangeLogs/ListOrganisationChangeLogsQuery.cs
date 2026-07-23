namespace KulturHub.Application.Features.ChangeLogs.ListOrganisationChangeLogs;

public sealed record ListOrganisationChangeLogsQuery(Guid OrganisationId, int? Skip, int? Take);
