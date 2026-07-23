using FluentValidation;

namespace KulturHub.Application.Features.Memberships.DeleteOrganisationMembership;

public sealed class DeleteOrganisationMembershipInputValidator : AbstractValidator<DeleteOrganisationMembershipInput>
{
    public DeleteOrganisationMembershipInputValidator()
    {
        RuleFor(x => x.OrganisationId)
            .NotEmpty().WithMessage("OrganisationId is required.");

        RuleFor(x => x.MembershipId)
            .NotEmpty().WithMessage("MembershipId is required.");

        RuleFor(x => x.ActingUserId)
            .NotEmpty().WithMessage("ActingUserId is required.");
    }
}
