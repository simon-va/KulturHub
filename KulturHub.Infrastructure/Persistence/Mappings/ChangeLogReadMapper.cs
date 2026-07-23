using KulturHub.Application.Features.ChangeLogs.ListOrganisationChangeLogs;
using KulturHub.Infrastructure.Persistence.Models;

namespace KulturHub.Infrastructure.Persistence.Mappings;

public static class ChangeLogReadMapper
{
    public static ChangeLogListItem ToListItem(ChangeLogReadRow row) =>
        new(
            row.Id,
            row.OrganisationId,
            row.UserId,
            BuildFullName(row.UserFirstName, row.UserLastName),
            row.Message,
            ChangeLogMapper.DeserializeData(row.Data),
            DateTime.SpecifyKind(row.CreatedAt, DateTimeKind.Utc));

    private static string BuildFullName(string firstName, string lastName) =>
        $"{firstName} {lastName}".Trim();
}
