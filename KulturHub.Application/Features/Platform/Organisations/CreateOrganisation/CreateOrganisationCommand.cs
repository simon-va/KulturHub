namespace KulturHub.Application.Features.Platform.Organisations.CreateOrganisation;

public sealed record CreateOrganisationCommand(Guid UserId, string Name);
