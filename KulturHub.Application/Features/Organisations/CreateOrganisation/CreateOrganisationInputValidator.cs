using FluentValidation;

namespace KulturHub.Application.Features.Organisations.CreateOrganisation;

public sealed class CreateOrganisationInputValidator : AbstractValidator<CreateOrganisationInput>
{
    public CreateOrganisationInputValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters.");
    }
}
