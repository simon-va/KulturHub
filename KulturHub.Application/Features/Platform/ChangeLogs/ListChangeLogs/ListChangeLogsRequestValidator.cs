using FluentValidation;
using KulturHub.Domain.ChangeLogs;

namespace KulturHub.Application.Features.Platform.ChangeLogs.ListChangeLogs;

public sealed class ListChangeLogsRequestValidator : AbstractValidator<ListChangeLogsRequest>
{
    public ListChangeLogsRequestValidator()
    {
        RuleFor(x => x.Skip).GreaterThanOrEqualTo(0);

        RuleFor(x => x.Take).InclusiveBetween(1, 200);

        RuleFor(x => x.Search)
            .MaximumLength(500)
            .When(x => x.Search is not null);

        RuleFor(x => x.Category)
            .Must(category => category is null || Enum.IsDefined(typeof(ChangeLogCategory), category.Value))
            .WithName("Category")
            .WithMessage("Category must be one of: 0 (Organisation), 1 (Events), 2 (Reports), 3 (Campaigns).")
            .When(x => x.Category is not null);
    }
}