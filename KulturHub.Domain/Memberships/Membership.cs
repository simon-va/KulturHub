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
        bool isDeleted)
    {
        Id = id;
        UserId = userId;
        OrganisationId = organisationId;
        JoinedAt = joinedAt;
        IsDeleted = isDeleted;
    }

    public MembershipId Id { get; }
    public UserId UserId { get; }
    public OrganisationId OrganisationId { get; }
    public DateTime JoinedAt { get; }
    public bool IsDeleted { get; private set; }

    public static ErrorOr<Membership> Create(
        UserId userId,
        OrganisationId organisationId,
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
            isDeleted: false);
    }
}
