using FluentValidation;
using KulturHub.Application.Rules;

namespace KulturHub.Application.Features.Auth.ValidateInvitation;

public sealed class ValidateInvitationInputValidator : AbstractValidator<ValidateInvitationInput>
{
    public ValidateInvitationInputValidator()
    {
        RuleFor(x => x.InvitationCode)
            .NotEmpty().WithMessage("InvitationCode is required.")
            .Matches(InvitationCodeRules.CodePattern)
            .WithMessage("InvitationCode must be in the format XXX-XXX using letters and digits excluding 0, O, 1, and I.");
    }
}
