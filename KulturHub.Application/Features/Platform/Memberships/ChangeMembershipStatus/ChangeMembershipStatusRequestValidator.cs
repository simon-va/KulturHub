using FluentValidation;

namespace KulturHub.Application.Features.Platform.Memberships.ChangeMembershipStatus;

public sealed class ChangeMembershipStatusRequestValidator : AbstractValidator<ChangeMembershipStatusRequest>
{
    public ChangeMembershipStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .IsInEnum();
    }
}