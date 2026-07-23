using ErrorOr;
using FluentValidation;
using KulturHub.Application.Errors;
using KulturHub.Application.Ports;
using KulturHub.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace KulturHub.Application.Features.Organisations.UpdateOrganisation;

public sealed class UpdateOrganisationHandler(
    IOrganisationRepository organisationRepository,
    IChangeLogRepository changeLogRepository,
    IUnitOfWork unitOfWork,
    IValidator<UpdateOrganisationInput> validator,
    ILogger<UpdateOrganisationHandler> logger)
{
    public async Task<ErrorOr<UpdateOrganisationResponse>> ExecuteAsync(
        UpdateOrganisationInput input,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await validator.ValidateAsync(input, cancellationToken);
        if (!validationResult.IsValid)
            return validationResult.Errors
                .Select(e => Error.Validation(e.PropertyName, e.ErrorMessage))
                .ToList();

        var organisation = await organisationRepository.GetByIdAsync(input.OrganisationId, cancellationToken);
        if (organisation is null)
            return OrganisationErrors.NotFound;

        var trimmedName = (input.Name ?? string.Empty).Trim();
        if (!string.Equals(organisation.Name, trimmedName, StringComparison.Ordinal)
            && await organisationRepository.ExistsByNameAsync(trimmedName, cancellationToken))
        {
            return OrganisationErrors.NameTaken;
        }

        var nameChanged = !string.Equals(organisation.Name, trimmedName, StringComparison.Ordinal);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            organisation.Rename(trimmedName);

            await organisationRepository.UpdateAsync(organisation, transaction, cancellationToken);

            if (nameChanged)
            {
                var changeLog = ChangeLog.Create(
                    organisation.Id,
                    input.UserId,
                    "Organisation aktualisiert",
                    new Dictionary<string, object?> { ["name"] = trimmedName });

                await changeLogRepository.InsertAsync(changeLog, transaction, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update organisation {OrganisationId} for user {UserId}.",
                organisation.Id, input.UserId);
            return OrganisationErrors.UpdateFailed(ex.Message);
        }

        return UpdateOrganisationResponse.From(organisation);
    }
}
