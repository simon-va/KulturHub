namespace KulturHub.Application.Features.Platform.Organisations.CreateOrganisation;

public sealed record CreateOrganisationResponse(
    Guid Id,
    string Name,
    DateTime CreatedAt);
