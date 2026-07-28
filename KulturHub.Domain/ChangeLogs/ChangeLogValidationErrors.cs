using ErrorOr;

namespace KulturHub.Domain.ChangeLogs;

internal static class ChangeLogValidationErrors
{
    public static readonly Error OrganisationIdRequired =
        Error.Validation("ChangeLog.OrganisationIdRequired", "OrganisationId is required.");

    public static readonly Error CreatedByRequired =
        Error.Validation("ChangeLog.CreatedByRequired", "CreatedBy is required.");

    public static readonly Error MessageRequired =
        Error.Validation("ChangeLog.MessageRequired", "Message is required.");

    public static readonly Error MessageTooLong =
        Error.Validation(
            "ChangeLog.MessageTooLong",
            $"Message must not exceed {ChangeLog.MaxMessageLength} characters.");

    public static readonly Error DataRequired =
        Error.Validation("ChangeLog.DataRequired", "Data is required.");

    public static readonly Error CreatedAtMustBeUtc =
        Error.Validation("ChangeLog.CreatedAtMustBeUtc", "CreatedAt must be UTC.");
}
