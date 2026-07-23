using ErrorOr;
using KulturHub.Application.Ports;

namespace KulturHub.Application.Features.Memberships.ListMyPendingMemberships;

public sealed class ListMyPendingMembershipsHandler(IMembershipRepository membershipRepository)
{
    public async Task<ErrorOr<IReadOnlyList<PendingMembershipListItem>>> ExecuteAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            return Error.Validation("UserId", "UserId is required.");

        var items = await membershipRepository.ListPendingByUserIdAsync(userId, cancellationToken);

        return items.ToList();
    }
}
