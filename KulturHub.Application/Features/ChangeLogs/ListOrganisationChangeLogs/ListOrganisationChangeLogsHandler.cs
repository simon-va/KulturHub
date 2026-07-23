using ErrorOr;
using FluentValidation;
using KulturHub.Application.Ports;
using Microsoft.Extensions.Logging;

namespace KulturHub.Application.Features.ChangeLogs.ListOrganisationChangeLogs;

public sealed class ListOrganisationChangeLogsHandler(
    IChangeLogRepository changeLogRepository,
    IValidator<ListOrganisationChangeLogsQuery> validator,
    ILogger<ListOrganisationChangeLogsHandler> logger)
{
    public async Task<ErrorOr<IReadOnlyList<ChangeLogListItem>>> ExecuteAsync(
        ListOrganisationChangeLogsQuery query,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await validator.ValidateAsync(query, cancellationToken);
        if (!validationResult.IsValid)
        {
            logger.LogDebug("Validation failed for ListOrganisationChangeLogsQuery: {Errors}",
                string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage)));
            return validationResult.Errors
                .Select(e => Error.Validation(e.PropertyName, e.ErrorMessage))
                .ToList();
        }

        var skip = query.Skip ?? ListOrganisationChangeLogsQueryValidator.MinSkip;
        var take = query.Take ?? ListOrganisationChangeLogsQueryValidator.DefaultTake;

        var items = await changeLogRepository.ListByOrganisationAsync(
            query.OrganisationId, skip, take, cancellationToken);

        return items.ToList();
    }
}
