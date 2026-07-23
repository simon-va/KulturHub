using FluentValidation;
using KulturHub.Application.Rules;

namespace KulturHub.Application.Features.Auth.SignUp;

public sealed class SignUpInputValidator : AbstractValidator<SignUpInput>
{
    public SignUpInputValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("FirstName is required.")
            .MaximumLength(100).WithMessage("FirstName must not exceed 100 characters.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("LastName is required.")
            .MaximumLength(100).WithMessage("LastName must not exceed 100 characters.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email must be a valid email address.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.");

        RuleFor(x => x.InvitationCode)
            .NotEmpty().WithMessage("InvitationCode is required.")
            .Matches(InvitationCodeRules.CodePattern)
            .WithMessage("InvitationCode must be in the format XXX-XXX using letters and digits excluding 0, O, 1, and I.");
    }
}
