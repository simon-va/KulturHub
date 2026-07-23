using ErrorOr;
using FluentValidation;
using KulturHub.Application.Errors;
using KulturHub.Application.Ports;
using KulturHub.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace KulturHub.Application.Features.Memberships.InviteMember;

public sealed class InviteMemberHandler(
    IMembershipRepository membershipRepository,
    IUserRepository userRepository,
    IChangeLogRepository changeLogRepository,
    IUnitOfWork unitOfWork,
    IValidator<InviteMemberInput> validator,
    ILogger<InviteMemberHandler> logger)
{
    public async Task<ErrorOr<InvitedMember>> ExecuteAsync(
        InviteMemberInput input,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await validator.ValidateAsync(input, cancellationToken);
        if (!validationResult.IsValid)
            return validationResult.Errors
                .Select(e => Error.Validation(e.PropertyName, e.ErrorMessage))
                .ToList();

        var invitedUser = await userRepository.GetByEmailAsync(input.Email, cancellationToken);
        if (invitedUser is null)
            return MembershipErrors.UserNotFound;

        if (invitedUser.UserId == input.ActingUserId)
            return MembershipErrors.SelfInvite;

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var existing = await membershipRepository.GetActiveByUserAndOrganisationAsync(
                invitedUser.UserId, input.OrganisationId, transaction, cancellationToken);

            if (existing is not null)
                return MembershipErrors.AlreadyInvited;

            var membership = Membership.Create(
                invitedUser.UserId,
                input.OrganisationId,
                MembershipStatus.Pending,
                invitedBy: input.ActingUserId);

            await membershipRepository.InsertAsync(membership, transaction, cancellationToken);

            var changeLog = ChangeLog.Create(
                input.OrganisationId,
                input.ActingUserId,
                "Member eingeladen",
                new Dictionary<string, object?>
                {
                    ["userId"] = invitedUser.UserId,
                    ["membershipId"] = membership.Id,
                    ["email"] = invitedUser.Email,
                });

            await changeLogRepository.InsertAsync(changeLog, transaction, cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return new InvitedMember(
                membership.Id,
                membership.UserId,
                invitedUser.Email,
                membership.Status);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to invite user {Email} to organisation {OrganisationId}.",
                input.Email, input.OrganisationId);
            return MembershipErrors.InviteFailed(ex.Message);
        }
    }
}
