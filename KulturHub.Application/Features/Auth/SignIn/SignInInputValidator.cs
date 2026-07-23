using FluentValidation;

namespace KulturHub.Application.Features.Auth.SignIn;

public sealed class SignInInputValidator : AbstractValidator<SignInInput>
{
    public SignInInputValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email must be a valid email address.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.");
    }
}
