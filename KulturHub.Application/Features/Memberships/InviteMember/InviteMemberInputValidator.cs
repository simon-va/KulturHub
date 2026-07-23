using FluentValidation;

namespace KulturHub.Application.Features.Memberships.InviteMember;

public sealed class InviteMemberInputValidator : AbstractValidator<InviteMemberInput>
{
    public InviteMemberInputValidator()
    {
        RuleFor(x => x.OrganisationId)
            .NotEmpty().WithMessage("OrganisationId is required.");

        RuleFor(x => x.ActingUserId)
            .NotEmpty().WithMessage("ActingUserId is required.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email must be a valid email address.");
    }
}
