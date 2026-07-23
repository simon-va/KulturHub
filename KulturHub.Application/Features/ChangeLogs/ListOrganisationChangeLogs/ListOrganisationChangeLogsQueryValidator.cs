using FluentValidation;

namespace KulturHub.Application.Features.ChangeLogs.ListOrganisationChangeLogs;

public sealed class ListOrganisationChangeLogsQueryValidator : AbstractValidator<ListOrganisationChangeLogsQuery>
{
    public const int DefaultTake = 50;
    public const int MinTake = 1;
    public const int MaxTake = 200;
    public const int MinSkip = 0;

    public ListOrganisationChangeLogsQueryValidator()
    {
        RuleFor(x => x.OrganisationId)
            .NotEmpty().WithMessage("OrganisationId is required.");

        RuleFor(x => x.Skip)
            .GreaterThanOrEqualTo(MinSkip)
            .WithMessage($"Skip must not be negative.")
            .When(x => x.Skip.HasValue);

        RuleFor(x => x.Take)
            .InclusiveBetween(MinTake, MaxTake)
            .WithMessage($"Take must be between {MinTake} and {MaxTake}.")
            .When(x => x.Take.HasValue);
    }
}
