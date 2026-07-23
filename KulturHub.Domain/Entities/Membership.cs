namespace KulturHub.Domain.Entities;

public class Membership
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid OrganisationId { get; private set; }
    public DateTime JoinedAt { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public MembershipStatus Status { get; private set; }
    public Guid? InvitedBy { get; private set; }

    private Membership() { }

    public static Membership Create(
        Guid userId,
        Guid organisationId,
        MembershipStatus status = MembershipStatus.Pending,
        Guid? invitedBy = null)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId is required.", nameof(userId));
        if (organisationId == Guid.Empty)
            throw new ArgumentException("OrganisationId is required.", nameof(organisationId));

        return new Membership
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OrganisationId = organisationId,
            JoinedAt = DateTime.UtcNow,
            Status = status,
            InvitedBy = invitedBy,
        };
    }

    public static Membership Reconstitute(
        Guid id,
        Guid userId,
        Guid organisationId,
        DateTime joinedAt,
        bool isDeleted = false,
        DateTime? deletedAt = null,
        MembershipStatus status = MembershipStatus.Accepted,
        Guid? invitedBy = null) => new()
    {
        Id = id,
        UserId = userId,
        OrganisationId = organisationId,
        JoinedAt = joinedAt,
        IsDeleted = isDeleted,
        DeletedAt = deletedAt,
        Status = status,
        InvitedBy = invitedBy,
    };

    public void MarkAsDeleted()
    {
        if (IsDeleted)
            throw new InvalidOperationException($"Membership {Id} is already deleted.");

        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
    }

    public void UpdateStatus(MembershipStatus newStatus)
    {
        if (Status != MembershipStatus.Pending)
            throw new InvalidOperationException(
                $"Membership {Id} status can only be changed from Pending.");

        Status = newStatus;
    }
}
