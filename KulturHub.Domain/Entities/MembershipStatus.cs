namespace KulturHub.Domain.Entities;

/// <summary>
/// Lifecycle status of a <see cref="Membership"/> in relation to its organisation.
/// Stored as <see cref="short"/> in the database to keep the membership table compact
/// and to match the small numeric range required for a status enum.
/// </summary>
public enum MembershipStatus : short
{
    Pending = 0,
    Accepted = 1,
    Rejected = 2,
}
