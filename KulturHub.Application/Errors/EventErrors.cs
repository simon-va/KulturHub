using ErrorOr;

namespace KulturHub.Application.Errors;

public static class EventErrors
{
    public static Error NotFound(Guid id) =>
        Error.NotFound("Event.NotFound", $"Event with id '{id}' was not found.");

    public static Error NoConversation(Guid id) =>
        Error.NotFound("Event.NoConversation", $"Event with id '{id}' has no conversation.");
}
