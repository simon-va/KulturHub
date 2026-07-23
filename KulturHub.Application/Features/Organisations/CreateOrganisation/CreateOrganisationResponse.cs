using KulturHub.Domain.Entities;

namespace KulturHub.Application.Features.Organisations.CreateOrganisation;

public sealed record CreateOrganisationResponse(
    Guid Id,
    string Name,
    DateTime CreatedAt)
{
    public static CreateOrganisationResponse From(Organisation organisation) => new(
        organisation.Id,
        organisation.Name,
        organisation.CreatedAt);
}
