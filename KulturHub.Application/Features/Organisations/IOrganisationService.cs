using ErrorOr;
using KulturHub.Application.Features.Organisations.CreateOrganisation;
using KulturHub.Application.Features.Organisations.GetOrganisations;
using KulturHub.Application.Features.Organisations.UpdateOrganisation;

namespace KulturHub.Application.Features.Organisations;

public interface IOrganisationService
{
    Task<ErrorOr<Guid>> CreateAsync(CreateOrganisationInput input);
    Task<ErrorOr<Updated>> UpdateAsync(Guid id, UpdateOrganisationInput input);
    Task<ErrorOr<Deleted>> DeleteAsync(Guid id, Guid userId);
    Task<IEnumerable<OrganisationResponse>> GetByUserIdAsync(Guid userId);
}
