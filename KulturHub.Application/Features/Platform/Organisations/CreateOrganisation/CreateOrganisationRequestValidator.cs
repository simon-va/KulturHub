using FluentValidation;
using KulturHub.Domain.Organisations;

namespace KulturHub.Application.Features.Platform.Organisations.CreateOrganisation;

public sealed class CreateOrganisationRequestValidator : AbstractValidator<CreateOrganisationRequest>
{
    public CreateOrganisationRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(Organisation.MaxNameLength);
    }
}
