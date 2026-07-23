namespace KulturHub.Application.Features.Organisations.UpdateOrganisation;

public sealed record UpdateOrganisationInput(string Name, Guid UserId, Guid OrganisationId);
