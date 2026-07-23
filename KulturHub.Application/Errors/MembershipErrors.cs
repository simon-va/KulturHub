using ErrorOr;

namespace KulturHub.Application.Errors;

public static class MembershipErrors
{
    public static readonly Error NotFound =
        Error.NotFound("Membership.NotFound", "Membership was not found.");

    public static readonly Error AlreadyDeleted =
        Error.Conflict("Membership.AlreadyDeleted", "Membership is already deleted.");

    public static readonly Error LastMember =
        Error.Conflict("Membership.LastMember", "Cannot delete the last active member of an organisation.");

    public static readonly Error UserNotFound =
        Error.NotFound("Membership.UserNotFound", "Invited user does not exist.");

    public static readonly Error AlreadyInvited =
        Error.Conflict("Membership.AlreadyInvited", "User is already a member or has a pending invite.");

    public static readonly Error SelfInvite =
        Error.Validation("Membership.SelfInvite", "You cannot invite yourself.");

    public static Error DeleteFailed(string details) =>
        Error.Failure("Membership.DeleteFailed", $"Failed to delete membership: {details}");

    public static Error InviteFailed(string details) =>
        Error.Failure("Membership.InviteFailed", $"Failed to invite member: {details}");

    public static readonly Error NotInvitee =
        Error.Forbidden("Membership.NotInvitee", "You cannot respond to an invitation that is not addressed to you.");

    public static readonly Error AlreadyDecided =
        Error.Conflict("Membership.AlreadyDecided", "This invitation has already been decided.");

    public static Error RespondFailed(string details) =>
        Error.Failure("Membership.RespondFailed", $"Failed to respond to membership: {details}");
}
