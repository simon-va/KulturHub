using KulturHub.Application.Features.Invitations.ListInvitations;
using KulturHub.Infrastructure.Persistence.Models;

namespace KulturHub.Infrastructure.Persistence.Mappings;

public static class InvitationReadMapper
{
    public static InvitationListItem ToListItem(InvitationReadRow row) =>
        new(
            row.Id,
            row.Code,
            DateTime.SpecifyKind(row.CreatedAt, DateTimeKind.Utc),
            DateTime.SpecifyKind(row.ExpiresAt, DateTimeKind.Utc),
            row.IsUsed,
            row.IsExpired,
            row.UsedById,
            BuildFullName(row.UsedByFirstName, row.UsedByLastName));

    private static string? BuildFullName(string? firstName, string? lastName)
    {
        if (string.IsNullOrWhiteSpace(firstName) && string.IsNullOrWhiteSpace(lastName))
            return null;

        return $"{firstName} {lastName}".Trim();
    }
}
