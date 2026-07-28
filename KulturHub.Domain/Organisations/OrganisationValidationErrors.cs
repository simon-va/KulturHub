using ErrorOr;

namespace KulturHub.Domain.Organisations;

internal static class OrganisationValidationErrors
{
    public static readonly Error NameRequired =
        Error.Validation("Organisation.NameRequired", "Name is required.");

    public static readonly Error NameTooLong =
        Error.Validation("Organisation.NameTooLong", "Name must not exceed 200 characters.");

    public static readonly Error CreatedAtMustBeUtc =
        Error.Validation("Organisation.CreatedAtMustBeUtc", "CreatedAt must be UTC.");
}
