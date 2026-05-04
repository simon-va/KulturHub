using ErrorOr;
using KulturHub.Application.Features.Organisations.CreateOrganisation;
using KulturHub.Application.Features.Organisations.UpdateOrganisation;

namespace KulturHub.Application.Features.Organisations;

public interface IOrganisationService
{
    Task<ErrorOr<Guid>> CreateAsync(CreateOrganisationInput input);
    Task<ErrorOr<Updated>> UpdateAsync(UpdateOrganisationInput input);
    Task<ErrorOr<Deleted>> DeleteAsync(Guid id, Guid userId);
}
