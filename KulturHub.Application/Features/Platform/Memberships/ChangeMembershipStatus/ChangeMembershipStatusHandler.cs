using ErrorOr;
using KulturHub.Application.Abstractions.Persistence;
using KulturHub.Application.Errors;
using KulturHub.Application.Features.Platform.Memberships.ListMemberships;
using KulturHub.Domain.ChangeLogs;
using KulturHub.Domain.Memberships;
using KulturHub.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KulturHub.Application.Features.Platform.Memberships.ChangeMembershipStatus;

public sealed class ChangeMembershipStatusHandler(
    IAppDbContext db,
    TimeProvider clock,
    ILogger<ChangeMembershipStatusHandler> logger)
{
    public async Task<ErrorOr<MembershipResponse>> HandleAsync(
        ChangeMembershipStatusCommand command,
        CancellationToken cancellationToken)
    {
        var membershipId = MembershipId.From(command.MembershipId);

        var membership = await db.Memberships
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.Id == membershipId, cancellationToken);

        if (membership is null || membership.IsDeleted)
            return MembershipErrors.NotFound;

        if (membership.UserId != UserId.From(command.CallerUserId))
            return MembershipErrors.Forbidden;

        if (membership.Status != MembershipStatus.Pending)
            return MembershipErrors.MustBePending;

        var transitionResult = command.NewStatus switch
        {
            MembershipChangeStatus.Accepted => membership.Accept(clock),
            MembershipChangeStatus.Rejected => membership.Reject(clock),
            _ => MembershipErrors.MustBePending,
        };

        if (transitionResult.IsError)
            return transitionResult.Errors;

        var user = await db.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == membership.UserId, cancellationToken);

        if (user is null)
            return MembershipErrors.UserNotFoundByEmail;

        var changeLogResult = ChangeLog.Create(
            membership.OrganisationId,
            membership.UserId,
            command.NewStatus == MembershipChangeStatus.Accepted
                ? "Einladung angenommen"
                : "Einladung abgelehnt",
            new Dictionary<string, string?>
            {
                ["from"] = nameof(MembershipStatus.Pending),
                ["to"] = ToStatus(command.NewStatus).ToString(),
            },
            clock);

        if (changeLogResult.IsError)
            return changeLogResult.Errors;

        db.ChangeLogs.Add(changeLogResult.Value);

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Membership {MembershipId} status changed to {Status} by {CallerUserId}",
            membership.Id.Value, command.NewStatus, command.CallerUserId);

        return new MembershipResponse(
            membership.Id.Value,
            membership.UserId.Value,
            FullName: $"{user.FirstName} {user.LastName}",
            user.Email,
            membership.InvitedAt,
            membership.DecidedAt,
            membership.Status);
    }

    private static MembershipStatus ToStatus(MembershipChangeStatus status) => status switch
    {
        MembershipChangeStatus.Accepted => MembershipStatus.Accepted,
        MembershipChangeStatus.Rejected => MembershipStatus.Rejected,
        _ => MembershipStatus.Pending,
    };
}