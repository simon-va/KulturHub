using ErrorOr;

namespace KulturHub.Application.Errors;

public static class EventErrors
{
    public static Error ChaynsCreateFailed(string details) =>
        Error.Failure("Event.ChaynsCreateFailed", $"Failed to create event in Chayns: {details}");
}
