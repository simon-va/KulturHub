namespace KulturHub.Application.Features.Platform.Organisations.CreateOrganisation;

public sealed record CreateOrganisationRequest
{
    public string Name { get; init; }

    public CreateOrganisationRequest(string name)
    {
        Name = name?.Trim() ?? string.Empty;
    }
}
