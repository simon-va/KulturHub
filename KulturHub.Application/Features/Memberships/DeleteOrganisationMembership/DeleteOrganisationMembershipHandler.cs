using ErrorOr;
using FluentValidation;
using KulturHub.Application.Errors;
using KulturHub.Application.Ports;
using KulturHub.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace KulturHub.Application.Features.Memberships.DeleteOrganisationMembership;

public sealed class DeleteOrganisationMembershipHandler(
    IMembershipRepository membershipRepository,
    IChangeLogRepository changeLogRepository,
    IUnitOfWork unitOfWork,
    IValidator<DeleteOrganisationMembershipInput> validator,
    ILogger<DeleteOrganisationMembershipHandler> logger)
{
    public async Task<ErrorOr<Deleted>> ExecuteAsync(
        DeleteOrganisationMembershipInput input,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await validator.ValidateAsync(input, cancellationToken);
        if (!validationResult.IsValid)
            return validationResult.Errors
                .Select(e => Error.Validation(e.PropertyName, e.ErrorMessage))
                .ToList();

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var membership = await membershipRepository.GetByIdAsync(
                input.MembershipId, transaction, cancellationToken);

            if (membership is null || membership.OrganisationId != input.OrganisationId)
                return MembershipErrors.NotFound;

            if (membership.IsDeleted)
                return MembershipErrors.AlreadyDeleted;

            var activeCount = await membershipRepository.CountActiveByOrganisationAsync(
                input.OrganisationId, transaction, cancellationToken);

            if (activeCount <= 1)
                return MembershipErrors.LastMember;

            membership.MarkAsDeleted();
            var rows = await membershipRepository.SoftDeleteAsync(
                membership.Id, transaction, cancellationToken);

            if (rows == 0)
            {
                logger.LogWarning("Membership {MembershipId} could not be deleted (concurrent modification).",
                    membership.Id);
                return MembershipErrors.AlreadyDeleted;
            }

            var changeLog = ChangeLog.Create(
                input.OrganisationId,
                input.ActingUserId,
                "Member entfernt",
                new Dictionary<string, object?>
                {
                    ["userId"] = membership.UserId,
                    ["membershipId"] = membership.Id,
                });

            await changeLogRepository.InsertAsync(changeLog, transaction, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result.Deleted;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete membership {MembershipId} for user {UserId}.",
                input.MembershipId, input.ActingUserId);
            return MembershipErrors.DeleteFailed(ex.Message);
        }
    }
}
