using ErrorOr;
using KulturHub.Application.Ports;

namespace KulturHub.Application.Features.Memberships.ListOrganisationMemberships;

public sealed class ListOrganisationMembershipsHandler(IMembershipRepository membershipRepository)
{
    public async Task<ErrorOr<IReadOnlyList<MembershipListItem>>> ExecuteAsync(
        Guid organisationId,
        CancellationToken cancellationToken = default)
    {
        if (organisationId == Guid.Empty)
            return Error.Validation("OrganisationId", "OrganisationId is required.");

        var items = await membershipRepository.ListActiveByOrganisationIdAsync(
            organisationId, cancellationToken);

        return items.ToList();
    }
}
