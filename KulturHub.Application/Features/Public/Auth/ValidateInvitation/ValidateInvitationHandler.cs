using ErrorOr;
using KulturHub.Application.Abstractions.Persistence;
using KulturHub.Application.Errors;
using Microsoft.EntityFrameworkCore;

namespace KulturHub.Application.Features.Public.Auth.ValidateInvitation;

public sealed class ValidateInvitationHandler(
    IAppDbContext db,
    TimeProvider clock)
{
    public async Task<ErrorOr<Success>> HandleAsync(
        ValidateInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var invitation = await db.Invitations
            .FirstOrDefaultAsync(i => i.Code == request.InvitationCode, cancellationToken);

        if (invitation is null)
            return InvitationErrors.NotFound;

        if (invitation.IsUsed)
            return InvitationErrors.AlreadyUsed;

        var now = clock.GetUtcNow().UtcDateTime;
        if (invitation.ExpiresAt <= now)
            return InvitationErrors.Expired;

        return Result.Success;
    }
}
