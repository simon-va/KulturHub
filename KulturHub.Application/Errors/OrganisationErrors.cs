using ErrorOr;

namespace KulturHub.Application.Errors;

public static class OrganisationErrors
{
    public static readonly Error NameTaken =
        Error.Conflict("Organisation.NameTaken", "An organisation with this name already exists.");

    public static readonly Error NotFound =
        Error.NotFound("Organisation.NotFound", "Organisation was not found.");

    public static Error CreateFailed(string details) =>
        Error.Failure("Organisation.CreateFailed", $"Failed to create organisation: {details}");

    public static Error UpdateFailed(string details) =>
        Error.Failure("Organisation.UpdateFailed", $"Failed to update organisation: {details}");
}
