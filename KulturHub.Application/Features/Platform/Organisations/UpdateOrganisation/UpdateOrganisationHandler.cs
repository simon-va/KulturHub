using ErrorOr;
using KulturHub.Application.Abstractions.Persistence;
using KulturHub.Application.Errors;
using KulturHub.Domain.ChangeLogs;
using KulturHub.Domain.Organisations;
using KulturHub.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KulturHub.Application.Features.Platform.Organisations.UpdateOrganisation;

public sealed class UpdateOrganisationHandler(
    IAppDbContext db,
    TimeProvider clock,
    ILogger<UpdateOrganisationHandler> logger)
{
    public async Task<ErrorOr<Success>> HandleAsync(
        Guid userId,
        Guid organisationId,
        UpdateOrganisationRequest request,
        CancellationToken cancellationToken)
    {
        var organisation = await db.Organisations
            .FirstOrDefaultAsync(
                o => o.Id == OrganisationId.From(organisationId),
                cancellationToken);

        if (organisation is null)
            return OrganisationErrors.NotFound;

        var nameTakenByOtherOrganisation = await db.Organisations
            .AsNoTracking()
            .AnyAsync(
                o => o.Name == request.Name
                    && o.Id != OrganisationId.From(organisationId),
                cancellationToken);

        if (nameTakenByOtherOrganisation)
            return OrganisationErrors.NameAlreadyExists;

        var updateResult = organisation.Update(request.Name);
        if (updateResult.IsError)
            return updateResult.Errors;

        var changeLogResult = ChangeLog.Create(
            OrganisationId.From(organisationId),
            UserId.From(userId),
            "Organisation wurde aktualisiert",
            new Dictionary<string, string?> { ["name"] = request.Name },
            clock);

        if (changeLogResult.IsError)
            return changeLogResult.Errors;

        db.ChangeLogs.Add(changeLogResult.Value);

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Organisation updated: {OrganisationId} by {UserId}",
            organisationId, userId);

        return Result.Success;
    }
}
