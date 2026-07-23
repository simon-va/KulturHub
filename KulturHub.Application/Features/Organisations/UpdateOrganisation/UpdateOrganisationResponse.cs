using KulturHub.Domain.Entities;

namespace KulturHub.Application.Features.Organisations.UpdateOrganisation;

public sealed record UpdateOrganisationResponse(
    Guid Id,
    string Name,
    DateTime CreatedAt)
{
    public static UpdateOrganisationResponse From(Organisation organisation) => new(
        organisation.Id,
        organisation.Name,
        organisation.CreatedAt);
}
