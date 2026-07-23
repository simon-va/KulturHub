using FluentValidation;

namespace KulturHub.Application.Features.Organisations.UpdateOrganisation;

public sealed class UpdateOrganisationInputValidator : AbstractValidator<UpdateOrganisationInput>
{
    public UpdateOrganisationInputValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters.");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required.");

        RuleFor(x => x.OrganisationId)
            .NotEmpty().WithMessage("OrganisationId is required.");
    }
}
