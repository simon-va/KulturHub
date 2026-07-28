namespace KulturHub.Application.Ports;

public interface IMembershipReader
{
    /// <summary>
    /// Returns <c>true</c> if the user is an <strong>accepted</strong> member of the organisation.
    /// Pending or rejected memberships, as well as soft-deleted memberships, do not count.
    /// </summary>
    Task<bool> IsMemberAsync(
        Guid userId,
        Guid organisationId,
        CancellationToken cancellationToken);
}