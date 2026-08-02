using ErrorOr;
using KulturHub.Application.Abstractions.Persistence;
using KulturHub.Application.Errors;
using KulturHub.Domain.ChangeLogs;
using KulturHub.Domain.Memberships;
using KulturHub.Domain.Organisations;
using KulturHub.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KulturHub.Application.Features.Platform.Organisations.CreateOrganisation;

public sealed class CreateOrganisationHandler(
    IAppDbContext db,
    TimeProvider clock,
    ILogger<CreateOrganisationHandler> logger)
{
    public async Task<ErrorOr<CreateOrganisationResponse>> HandleAsync(
        CreateOrganisationCommand command,
        CancellationToken cancellationToken)
    {
        var nameAlreadyTaken = await db.Organisations
            .AsNoTracking()
            .AnyAsync(o => o.Name == command.Name, cancellationToken);

        if (nameAlreadyTaken)
            return OrganisationErrors.NameAlreadyExists;

        var createResult = Organisation.Create(command.Name, clock);
        if (createResult.IsError)
            return createResult.Errors;

        var organisation = createResult.Value;

        var membershipResult = Membership.Create(
            UserId.From(command.UserId),
            organisation.Id,
            MembershipStatus.Accepted,
            clock);

        if (membershipResult.IsError)
            return membershipResult.Errors;

        var changeLogResult = ChangeLog.Create(
            organisation.Id,
            UserId.From(command.UserId),
            "Organisation wurde erstellt",
            ChangeLogCategory.Organisation,
            new Dictionary<string, string?> { ["name"] = organisation.Name },
            clock);

        if (changeLogResult.IsError)
            return changeLogResult.Errors;

        db.Organisations.Add(organisation);
        db.Memberships.Add(membershipResult.Value);
        db.ChangeLogs.Add(changeLogResult.Value);

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Organisation created: {OrganisationId} by {UserId}",
            organisation.Id.Value, command.UserId);

        return new CreateOrganisationResponse(
            organisation.Id.Value,
            organisation.Name,
            organisation.CreatedAt);
    }
}
