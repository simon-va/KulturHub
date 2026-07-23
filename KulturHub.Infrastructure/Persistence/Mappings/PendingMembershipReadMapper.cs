using KulturHub.Application.Features.Memberships.ListMyPendingMemberships;
using KulturHub.Infrastructure.Persistence.Models;

namespace KulturHub.Infrastructure.Persistence.Mappings;

public static class PendingMembershipReadMapper
{
    public static PendingMembershipListItem ToListItem(PendingMembershipReadRow row) =>
        new(
            row.MembershipId,
            BuildFullName(row.InviterFirstName, row.InviterLastName),
            row.OrganisationName);

    private static string? BuildFullName(string? firstName, string? lastName)
    {
        if (string.IsNullOrWhiteSpace(firstName) && string.IsNullOrWhiteSpace(lastName))
            return null;

        return $"{firstName} {lastName}".Trim();
    }
}
