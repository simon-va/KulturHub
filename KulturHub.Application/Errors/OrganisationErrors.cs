using ErrorOr;

namespace KulturHub.Application.Errors;

public static class OrganisationErrors
{
    public static Error NotFound(Guid id) =>
        Error.NotFound("Organisation.NotFound", $"Organisation with id '{id}' was not found.");

    public static Error Forbidden() =>
        Error.Forbidden("Organisation.Forbidden", "You are not a member of this organisation.");
}
