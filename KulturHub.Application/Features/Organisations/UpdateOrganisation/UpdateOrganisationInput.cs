namespace KulturHub.Application.Features.Organisations.UpdateOrganisation;

public record UpdateOrganisationInput(Guid Id, string Name, Guid UserId);
