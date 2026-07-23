using ErrorOr;
using FluentValidation;
using KulturHub.Application.Errors;
using KulturHub.Application.Ports;
using KulturHub.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace KulturHub.Application.Features.Memberships.RespondToMembership;

public sealed class RespondToMembershipHandler(
    IMembershipRepository membershipRepository,
    IChangeLogRepository changeLogRepository,
    IUnitOfWork unitOfWork,
    IValidator<RespondToMembershipInput> validator,
    ILogger<RespondToMembershipHandler> logger)
{
    public async Task<ErrorOr<Success>> ExecuteAsync(
        RespondToMembershipInput input,
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

            if (membership is null)
                return MembershipErrors.NotFound;

            if (membership.UserId != input.ActingUserId)
                return MembershipErrors.NotInvitee;

            if (membership.IsDeleted)
                return MembershipErrors.AlreadyDeleted;

            if (membership.Status != MembershipStatus.Pending)
                return MembershipErrors.AlreadyDecided;

            var newStatus = ToDomainStatus(input.Decision);
            membership.UpdateStatus(newStatus);

            var rows = await membershipRepository.UpdateStatusAsync(
                membership.Id, newStatus, transaction, cancellationToken);

            if (rows == 0)
            {
                logger.LogWarning("Membership {MembershipId} could not be updated (concurrent modification).",
                    membership.Id);
                return MembershipErrors.AlreadyDecided;
            }

            var (message, statusLabel) = input.Decision switch
            {
                MembershipDecision.Accept => ("Einladung angenommen", "Accepted"),
                MembershipDecision.Reject => ("Einladung abgelehnt", "Rejected"),
                _ => throw new InvalidOperationException($"Unhandled decision {input.Decision}."),
            };

            var changeLog = ChangeLog.Create(
                membership.OrganisationId,
                input.ActingUserId,
                message,
                new Dictionary<string, object?>
                {
                    ["userId"] = membership.UserId,
                    ["membershipId"] = membership.Id,
                    ["status"] = statusLabel,
                });

            await changeLogRepository.InsertAsync(changeLog, transaction, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result.Success;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to respond to membership {MembershipId} for user {UserId}.",
                input.MembershipId, input.ActingUserId);
            return MembershipErrors.RespondFailed(ex.Message);
        }
    }

    private static MembershipStatus ToDomainStatus(MembershipDecision decision) => decision switch
    {
        MembershipDecision.Accept => MembershipStatus.Accepted,
        MembershipDecision.Reject => MembershipStatus.Rejected,
        _ => throw new InvalidOperationException($"Unhandled decision {decision}."),
    };
}
