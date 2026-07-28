namespace KulturHub.Application.Features.Platform.Organisations.UpdateOrganisation;

public sealed record UpdateOrganisationRequest
{
    public string Name { get; init; }

    public UpdateOrganisationRequest(string name)
    {
        Name = name?.Trim() ?? string.Empty;
    }
}
