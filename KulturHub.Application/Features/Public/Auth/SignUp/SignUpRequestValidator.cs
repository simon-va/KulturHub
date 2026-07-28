using FluentValidation;

namespace KulturHub.Application.Features.Public.Auth.SignUp;

public sealed class SignUpRequestValidator : AbstractValidator<SignUpRequest>
{
    private const string InvitationCodePattern = @"^[A-HJ-NP-Z2-9]{3}-[A-HJ-NP-Z2-9]{3}$";

    public SignUpRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(255);

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(128);

        RuleFor(x => x.FirstName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.LastName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.InvitationCode)
            .NotEmpty()
            .Matches(InvitationCodePattern)
            .WithMessage("Invitation code must match the format 'XXX-XXX' using A-Z (without I and O) and 2-9.");
    }
}