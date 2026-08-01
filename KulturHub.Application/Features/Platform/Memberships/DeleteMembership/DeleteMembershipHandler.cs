using ErrorOr;
using KulturHub.Application.Abstractions.Persistence;
using KulturHub.Application.Errors;
using KulturHub.Domain.ChangeLogs;
using KulturHub.Domain.Memberships;
using KulturHub.Domain.Organisations;
using KulturHub.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KulturHub.Application.Features.Platform.Memberships.DeleteMembership;

public sealed class DeleteMembershipHandler(
    IAppDbContext db,
    TimeProvider clock,
    ILogger<DeleteMembershipHandler> logger)
{
    public async Task<ErrorOr<Success>> HandleAsync(
        DeleteMembershipCommand command,
        CancellationToken cancellationToken)
    {
        var membershipId = MembershipId.From(command.MembershipId);
        var organisationId = OrganisationId.From(command.OrganisationId);
        var actorUserId = UserId.From(command.ActorUserId);

        var membership = await db.Memberships
            .FirstOrDefaultAsync(m => m.Id == membershipId, cancellationToken);

        if (membership is null)
            return MembershipErrors.NotFound;

        if (membership.OrganisationId != organisationId)
            return MembershipErrors.NotFound;

        if (membership.Status == MembershipStatus.Accepted)
        {
            var activeMemberCount = await db.Memberships
                .CountAsync(
                    m => m.OrganisationId == organisationId
                        && m.Status == MembershipStatus.Accepted,
                    cancellationToken);

            if (activeMemberCount <= 1)
                return MembershipErrors.LastActiveMember;
        }

        var deleteResult = membership.Delete(clock);
        if (deleteResult.IsError)
            return deleteResult.Errors;

        var changeLogResult = ChangeLog.Create(
            organisationId,
            actorUserId,
            "Mitglied entfernt",
            new Dictionary<string, string?>
            {
                ["membershipId"] = membership.Id.Value.ToString(),
                ["userId"] = membership.UserId.Value.ToString(),
                ["status"] = membership.Status.ToString(),
            },
            clock);

        if (changeLogResult.IsError)
            return changeLogResult.Errors;

        db.ChangeLogs.Add(changeLogResult.Value);

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Membership {MembershipId} deleted from organisation {OrganisationId} by {ActorUserId}",
            membership.Id.Value, command.OrganisationId, command.ActorUserId);

        return Result.Success;
    }
}
