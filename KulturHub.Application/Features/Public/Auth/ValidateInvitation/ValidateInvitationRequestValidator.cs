using FluentValidation;

namespace KulturHub.Application.Features.Public.Auth.ValidateInvitation;

public sealed class ValidateInvitationRequestValidator : AbstractValidator<ValidateInvitationRequest>
{
    private const string InvitationCodePattern = @"^[A-HJ-NP-Z2-9]{3}-[A-HJ-NP-Z2-9]{3}$";

    public ValidateInvitationRequestValidator()
    {
        RuleFor(x => x.InvitationCode)
            .NotEmpty()
            .Matches(InvitationCodePattern)
            .WithMessage("Invitation code must match the format 'XXX-XXX' using A-Z (without I and O) and 2-9.");
    }
}
