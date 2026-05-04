using FluentValidation;

namespace KulturHub.Application.Features.Events.CreateEvent;

public sealed class CreateEventInputValidator : AbstractValidator<CreateEventInput>
{
    public CreateEventInputValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters.");

        RuleFor(x => x.StartTime)
            .NotEqual(default(DateTime)).WithMessage("StartTime is required.")
            .GreaterThan(DateTime.UtcNow).WithMessage("StartTime must be in the future.");

        RuleFor(x => x.EndTime)
            .NotEqual(default(DateTime)).WithMessage("EndTime is required.")
            .GreaterThan(x => x.StartTime).WithMessage("EndTime must be after StartTime.");

        RuleFor(x => x.Address)
            .NotEmpty().WithMessage("Address is required.")
            .MaximumLength(500).WithMessage("Address must not exceed 500 characters.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required.")
            .MaximumLength(2000).WithMessage("Description must not exceed 2000 characters.");
    }
}
