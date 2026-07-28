using FluentValidation;

namespace KulturHub.Application.Features.Platform.Memberships.InviteMembership;

public sealed class InviteMembershipRequestValidator : AbstractValidator<InviteMembershipRequest>
{
    public InviteMembershipRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(255);
    }
}