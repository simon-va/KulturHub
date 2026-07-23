using ErrorOr;
using KulturHub.Application.Ports;

namespace KulturHub.Application.Features.Organisations.ListUserOrganisations;

public sealed class ListUserOrganisationsHandler(IOrganisationRepository organisationRepository)
{
    public async Task<ErrorOr<IReadOnlyList<OrganisationSummary>>> ExecuteAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            return Error.Validation("UserId", "UserId is required.");

        var organisations = await organisationRepository.ListByUserIdAsync(userId, cancellationToken);

        return organisations.Select(OrganisationSummary.From).ToList();
    }
}
