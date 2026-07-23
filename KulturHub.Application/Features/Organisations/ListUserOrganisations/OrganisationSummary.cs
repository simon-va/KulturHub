using KulturHub.Domain.Entities;

namespace KulturHub.Application.Features.Organisations.ListUserOrganisations;

public sealed record OrganisationSummary(Guid Id, string Name)
{
    public static OrganisationSummary From(Organisation organisation) =>
        new(organisation.Id, organisation.Name);
}
