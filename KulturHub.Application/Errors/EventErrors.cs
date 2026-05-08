using ErrorOr;

namespace KulturHub.Application.Errors;

public static class EventErrors
{
    public static Error NotFound(Guid id) =>
        Error.NotFound("Event.NotFound", $"Event with id '{id}' was not found.");

    public static Error NoConversation(Guid id) =>
        Error.NotFound("Event.NoConversation", $"Event with id '{id}' has no conversation.");

    public static Error IncompleteAiResponse() =>
        Error.Failure("Event.IncompleteAiResponse", "AI returned a ready status but missing fields.");

    public static Error InvalidTransition(string from, string to) =>
        Error.Validation("Event.InvalidTransition", $"Cannot transition from '{from}' to '{to}'.");

    public static Error AiParseError() =>
        Error.Failure("Event.AiParseError", "AI returned an unparseable or invalid response.");

    public static Error ConcurrencyConflict() =>
        Error.Conflict("Event.ConcurrencyConflict", "Event was modified by another request. Please try again.");
}
