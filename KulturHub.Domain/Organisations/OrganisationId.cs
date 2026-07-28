namespace KulturHub.Domain.Organisations;

public readonly record struct OrganisationId(Guid Value)
{
    public static OrganisationId New() => new(Guid.NewGuid());

    public static OrganisationId From(Guid value) => new(value);
}
