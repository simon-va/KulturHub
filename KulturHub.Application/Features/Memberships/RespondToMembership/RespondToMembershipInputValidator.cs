using FluentValidation;

namespace KulturHub.Application.Features.Memberships.RespondToMembership;

public sealed class RespondToMembershipInputValidator : AbstractValidator<RespondToMembershipInput>
{
    public RespondToMembershipInputValidator()
    {
        RuleFor(x => x.MembershipId)
            .NotEmpty().WithMessage("MembershipId is required.");

        RuleFor(x => x.ActingUserId)
            .NotEmpty().WithMessage("ActingUserId is required.");

        RuleFor(x => x.Decision)
            .IsInEnum().WithMessage("Decision must be a valid membership decision.");
    }
}
