using ErrorOr;
using KulturHub.Application.Abstractions.Persistence;
using KulturHub.Application.Errors;
using KulturHub.Application.Ports;
using KulturHub.Domain.Invitations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KulturHub.Application.Features.Admin.Invitations.CreateInvitation;

public sealed class CreateInvitationHandler(
    IAppDbContext db,
    IInvitationCodeGenerator codeGenerator,
    TimeProvider clock,
    ILogger<CreateInvitationHandler> logger)
{
    private const int MaxCollisionRetries = 5;
    private const int ExpiryInDays = 30;

    public async Task<ErrorOr<CreateInvitationResponse>> HandleAsync(
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var expiresAt = now.AddDays(ExpiryInDays);

        for (var attempt = 1; attempt <= MaxCollisionRetries; attempt++)
        {
            var code = codeGenerator.Generate();
            var createResult = Invitation.Create(code, now, expiresAt);
            if (createResult.IsError)
                return createResult.Errors;

            var exists = await db.Invitations
                .AsNoTracking()
                .AnyAsync(i => i.Code == code, cancellationToken);

            if (exists)
                continue;

            db.Invitations.Add(createResult.Value);

            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                continue;
            }

            logger.LogInformation(
                "Invitation created: {InvitationId} (attempt {Attempt})",
                createResult.Value.Id.Value, attempt);

            return MapToResponse(createResult.Value);
        }

        logger.LogWarning(
            "Failed to generate a unique invitation code after {MaxRetries} attempts.",
            MaxCollisionRetries);

        return InvitationErrors.CodeGenerationFailed;
    }

    private static CreateInvitationResponse MapToResponse(Invitation invitation) =>
        new(invitation.Id.Value, invitation.Code, invitation.CreatedAt, invitation.ExpiresAt);
}
