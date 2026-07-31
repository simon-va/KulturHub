using FluentValidation;
using KulturHub.Domain.Invitations;

namespace KulturHub.Application.Features.Public.Auth.ValidateInvitation;

public sealed class ValidateInvitationRequestValidator : AbstractValidator<ValidateInvitationRequest>
{
    public ValidateInvitationRequestValidator()
    {
        RuleFor(x => x.InvitationCode)
            .NotEmpty()
            .Matches(InvitationCodeSpecs.Pattern)
            .WithMessage("Invitation code must match the format 'XXXX' using A-Z (without I and O) and 2-9.");
    }
}
