using ErrorOr;
using KulturHub.Application.Abstractions.Persistence;
using KulturHub.Application.Errors;
using KulturHub.Application.Ports;
using KulturHub.Domain.ChangeLogs;
using KulturHub.Domain.Memberships;
using KulturHub.Domain.Organisations;
using KulturHub.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KulturHub.Application.Features.Platform.Memberships.InviteMembership;

public sealed class InviteMembershipHandler(
    IAppDbContext db,
    IUserReader userReader,
    TimeProvider clock,
    ILogger<InviteMembershipHandler> logger)
{
    public async Task<ErrorOr<InviteMembershipResponse>> HandleAsync(
        InviteMembershipCommand command,
        CancellationToken cancellationToken)
    {
        var user = await userReader.GetByEmailAsync(command.Email, cancellationToken);
        if (user is null)
            return MembershipErrors.UserNotFoundByEmail;

        var membershipExists = await db.Memberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(
                m => m.OrganisationId == OrganisationId.From(command.OrganisationId)
                     && m.UserId == user.Id
                     && !m.IsDeleted,
                cancellationToken);

        if (membershipExists)
            return MembershipErrors.AlreadyExists;

        var membershipResult = Membership.Create(user.Id, OrganisationId.From(command.OrganisationId), clock);
        if (membershipResult.IsError)
            return membershipResult.Errors;

        var changeLogResult = ChangeLog.Create(
            OrganisationId.From(command.OrganisationId),
            UserId.From(command.InviterUserId),
            "Nutzer wurde eingeladen",
            new Dictionary<string, string?>
            {
                ["email"] = user.Email,
                ["status"] = MembershipStatus.Pending.ToString(),
            },
            clock);

        if (changeLogResult.IsError)
            return changeLogResult.Errors;

        db.Memberships.Add(membershipResult.Value);
        db.ChangeLogs.Add(changeLogResult.Value);

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "User {InviteeUserId} ({Email}) invited to organisation {OrganisationId} by {InviterUserId}",
            user.Id.Value, user.Email, command.OrganisationId, command.InviterUserId);

        return new InviteMembershipResponse(
            membershipResult.Value.Id.Value,
            user.Id.Value,
            FullName: $"{user.FirstName} {user.LastName}",
            user.Email,
            membershipResult.Value.JoinedAt,
            membershipResult.Value.Status);
    }
}