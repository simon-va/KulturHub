using ErrorOr;
using KulturHub.Application.Abstractions.Persistence;
using KulturHub.Application.Errors;
using KulturHub.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KulturHub.Application.Features.Platform.Users.GetCurrentUser;

public sealed class GetCurrentUserHandler(
    IAppDbContext db,
    ILogger<GetCurrentUserHandler> logger)
{
    public async Task<ErrorOr<MeResponse>> HandleAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == UserId.From(userId), cancellationToken);

        if (user is null)
        {
            logger.LogInformation("GetCurrentUser: user {UserId} not found", userId);
            return UserErrors.NotFound;
        }

        return new MeResponse(
            user.Id.Value,
            user.FirstName,
            user.LastName,
            user.Email,
            user.IsAdmin,
            user.CreatedAt);
    }
}
