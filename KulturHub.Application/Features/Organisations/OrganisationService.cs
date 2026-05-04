using ErrorOr;
using FluentValidation;
using KulturHub.Application.Errors;
using KulturHub.Application.Features.Organisations.CreateOrganisation;
using KulturHub.Application.Features.Organisations.UpdateOrganisation;
using KulturHub.Domain.Entities;
using KulturHub.Domain.Interfaces;

namespace KulturHub.Application.Features.Organisations;

public class OrganisationService(
    IOrganisationRepository organisationRepository,
    IValidator<CreateOrganisationInput> createValidator,
    IValidator<UpdateOrganisationInput> updateValidator) : IOrganisationService
{
    public async Task<ErrorOr<Guid>> CreateAsync(CreateOrganisationInput input)
    {
        var validationResult = await createValidator.ValidateAsync(input);
        if (!validationResult.IsValid)
            throw new ValidationException(validationResult.Errors);

        var organisation = Organisation.Create(input.Name);
        await organisationRepository.CreateAsync(organisation, input.UserId);

        return organisation.Id;
    }

    public async Task<ErrorOr<Updated>> UpdateAsync(UpdateOrganisationInput input)
    {
        var validationResult = await updateValidator.ValidateAsync(input);
        if (!validationResult.IsValid)
            throw new ValidationException(validationResult.Errors);

        var isMember = await organisationRepository.IsMemberAsync(input.Id, input.UserId);
        if (!isMember)
            return OrganisationErrors.Forbidden();

        var updated = await organisationRepository.UpdateAsync(input.Id, input.Name);
        if (!updated)
            return OrganisationErrors.NotFound(input.Id);

        return Result.Updated;
    }

    public async Task<ErrorOr<Deleted>> DeleteAsync(Guid id, Guid userId)
    {
        var isMember = await organisationRepository.IsMemberAsync(id, userId);
        if (!isMember)
            return OrganisationErrors.Forbidden();

        var deleted = await organisationRepository.DeleteAsync(id);
        if (!deleted)
            return OrganisationErrors.NotFound(id);

        return Result.Deleted;
    }
}
