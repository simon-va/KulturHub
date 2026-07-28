using FluentValidation;
using KulturHub.Domain.Organisations;

namespace KulturHub.Application.Features.Platform.Organisations.UpdateOrganisation;

public sealed class UpdateOrganisationRequestValidator : AbstractValidator<UpdateOrganisationRequest>
{
    public UpdateOrganisationRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(Organisation.MaxNameLength);
    }
}
