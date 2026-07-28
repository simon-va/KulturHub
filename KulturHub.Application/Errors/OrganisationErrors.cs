using ErrorOr;

namespace KulturHub.Application.Errors;

public static class OrganisationErrors
{
    public static readonly Error NameAlreadyExists =
        Error.Conflict("Organisation.NameAlreadyExists",
            "An organisation with this name already exists.");

    public static readonly Error NotFound =
        Error.NotFound("Organisation.NotFound",
            "Organisation was not found.");
}
