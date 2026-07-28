using ErrorOr;
using KulturHub.Domain.Organisations;
using KulturHub.Domain.Users;

namespace KulturHub.Domain.Memberships;

public sealed class Membership
{
    private Membership(
        MembershipId id,
        UserId userId,
        OrganisationId organisationId,
        DateTime invitedAt,
        DateTime? decidedAt,
        MembershipStatus status,
        bool isDeleted,
        DateTime? deletedAt)
    {
        Id = id;
        UserId = userId;
        OrganisationId = organisationId;
        InvitedAt = invitedAt;
        DecidedAt = decidedAt;
        Status = status;
        IsDeleted = isDeleted;
        DeletedAt = deletedAt;
    }

    public MembershipId Id { get; }
    public UserId UserId { get; }
    public OrganisationId OrganisationId { get; }
    public DateTime InvitedAt { get; }
    public DateTime? DecidedAt { get; private set; }
    public MembershipStatus Status { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    public static ErrorOr<Membership> Create(
        UserId userId,
        OrganisationId organisationId,
        MembershipStatus status,
        TimeProvider clock)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        if (now.Kind != DateTimeKind.Utc)
            return MembershipValidationErrors.InvitedAtMustBeUtc;

        var decidedAt = status == MembershipStatus.Pending ? null : (DateTime?)now;

        return new Membership(
            MembershipId.New(),
            userId,
            organisationId,
            invitedAt: now,
            decidedAt,
            status,
            isDeleted: false,
            deletedAt: null);
    }

    public ErrorOr<Success> Accept(TimeProvider clock)
    {
        if (Status != MembershipStatus.Pending)
            return MembershipValidationErrors.MustBePending;

        var now = clock.GetUtcNow().UtcDateTime;
        if (now.Kind != DateTimeKind.Utc)
            return MembershipValidationErrors.DecidedAtMustBeUtc;

        Status = MembershipStatus.Accepted;
        DecidedAt = now;
        return Result.Success;
    }

    public ErrorOr<Success> Reject(TimeProvider clock)
    {
        if (Status != MembershipStatus.Pending)
            return MembershipValidationErrors.MustBePending;

        var now = clock.GetUtcNow().UtcDateTime;
        if (now.Kind != DateTimeKind.Utc)
            return MembershipValidationErrors.DecidedAtMustBeUtc;

        Status = MembershipStatus.Rejected;
        DecidedAt = now;
        return Result.Success;
    }

    public ErrorOr<Success> Delete(TimeProvider clock)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        if (now.Kind != DateTimeKind.Utc)
            return MembershipValidationErrors.DeletedAtMustBeUtc;

        IsDeleted = true;
        DeletedAt = now;
        return Result.Success;
    }
}