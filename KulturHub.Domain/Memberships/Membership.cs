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
        DateTime joinedAt,
        MembershipStatus status,
        bool isDeleted)
    {
        Id = id;
        UserId = userId;
        OrganisationId = organisationId;
        JoinedAt = joinedAt;
        Status = status;
        IsDeleted = isDeleted;
    }

    public MembershipId Id { get; }
    public UserId UserId { get; }
    public OrganisationId OrganisationId { get; }
    public DateTime JoinedAt { get; }
    public MembershipStatus Status { get; private set; }
    public bool IsDeleted { get; private set; }

    public static ErrorOr<Membership> Create(
        UserId userId,
        OrganisationId organisationId,
        TimeProvider clock)
    {
        return CreateInternal(userId, organisationId, MembershipStatus.Pending, clock);
    }

    public static ErrorOr<Membership> CreateAccepted(
        UserId userId,
        OrganisationId organisationId,
        TimeProvider clock)
    {
        return CreateInternal(userId, organisationId, MembershipStatus.Accepted, clock);
    }

    private static ErrorOr<Membership> CreateInternal(
        UserId userId,
        OrganisationId organisationId,
        MembershipStatus status,
        TimeProvider clock)
    {
        var joinedAt = clock.GetUtcNow().UtcDateTime;
        if (joinedAt.Kind != DateTimeKind.Utc)
            return MembershipValidationErrors.JoinedAtMustBeUtc;

        return new Membership(
            MembershipId.New(),
            userId,
            organisationId,
            joinedAt,
            status,
            isDeleted: false);
    }
}