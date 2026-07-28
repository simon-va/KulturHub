namespace KulturHub.Application.Ports;

public interface IMembershipReader
{
    Task<bool> IsMemberAsync(
        Guid userId,
        Guid organisationId,
        CancellationToken cancellationToken);
}
