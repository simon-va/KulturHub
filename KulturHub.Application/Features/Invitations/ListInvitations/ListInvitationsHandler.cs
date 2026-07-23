using ErrorOr;
using KulturHub.Application.Ports;

namespace KulturHub.Application.Features.Invitations.ListInvitations;

public sealed class ListInvitationsHandler(IInvitationRepository invitationRepository)
{
    public async Task<ErrorOr<IReadOnlyList<InvitationListItem>>> ExecuteAsync(
        ListInvitationsQuery query,
        CancellationToken cancellationToken = default)
    {
        var items = await invitationRepository.ListAsync(
            new InvitationFilter(query.IncludeUsed, query.IncludeExpired),
            cancellationToken);

        return items.ToList();
    }
}
