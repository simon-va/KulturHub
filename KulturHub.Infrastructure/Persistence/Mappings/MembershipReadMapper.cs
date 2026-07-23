using KulturHub.Application.Features.Memberships.ListOrganisationMemberships;
using KulturHub.Infrastructure.Persistence.Models;

namespace KulturHub.Infrastructure.Persistence.Mappings;

public static class MembershipReadMapper
{
    public static MembershipListItem ToListItem(MembershipReadRow row) =>
        new(
            row.MembershipId,
            row.UserId,
            BuildFullName(row.FirstName, row.LastName),
            string.IsNullOrWhiteSpace(row.Email) ? null : row.Email,
            row.Status,
            DateTime.SpecifyKind(row.JoinedAt, DateTimeKind.Utc));

    private static string? BuildFullName(string? firstName, string? lastName)
    {
        if (string.IsNullOrWhiteSpace(firstName) && string.IsNullOrWhiteSpace(lastName))
            return null;

        return $"{firstName} {lastName}".Trim();
    }
}
