using ErrorOr;
using FluentValidation;
using KulturHub.Application.Errors;
using KulturHub.Application.Ports;
using KulturHub.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace KulturHub.Application.Features.Organisations.CreateOrganisation;

public sealed class CreateOrganisationHandler(
    IOrganisationRepository organisationRepository,
    IMembershipRepository membershipRepository,
    IChangeLogRepository changeLogRepository,
    IUnitOfWork unitOfWork,
    IValidator<CreateOrganisationInput> validator,
    ILogger<CreateOrganisationHandler> logger)
{
    public async Task<ErrorOr<CreateOrganisationResponse>> ExecuteAsync(
        CreateOrganisationInput input,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await validator.ValidateAsync(input, cancellationToken);
        if (!validationResult.IsValid)
            return validationResult.Errors
                .Select(e => Error.Validation(e.PropertyName, e.ErrorMessage))
                .ToList();

        if (await organisationRepository.ExistsByNameAsync(input.Name, cancellationToken))
            return OrganisationErrors.NameTaken;

        var organisation = Organisation.Create(input.Name);
        var ownerMembership = Membership.Create(input.UserId, organisation.Id, MembershipStatus.Accepted);
        var changeLog = ChangeLog.Create(
            organisation.Id,
            input.UserId,
            "Organisation erstellt",
            new Dictionary<string, object?> { ["name"] = organisation.Name });

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            await organisationRepository.InsertAsync(organisation, transaction, cancellationToken);
            await membershipRepository.InsertAsync(ownerMembership, transaction, cancellationToken);
            await changeLogRepository.InsertAsync(changeLog, transaction, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create organisation {OrganisationId} for user {UserId}.",
                organisation.Id, input.UserId);
            return OrganisationErrors.CreateFailed(ex.Message);
        }

        return CreateOrganisationResponse.From(organisation);
    }
}
