using KulturHub.Application.Features.Memberships.ListMyPendingMemberships;
using KulturHub.Application.Features.Memberships.ListOrganisationMemberships;
using KulturHub.Domain.Entities;

namespace KulturHub.Application.Ports;

public interface IMembershipRepository
{
    Task InsertAsync(
        Membership membership,
        IUnitOfWorkTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    Task<bool> IsMemberAsync(
        Guid userId,
        Guid organisationId,
        CancellationToken cancellationToken = default);

    Task<Membership?> GetByIdAsync(
        Guid id,
        IUnitOfWorkTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    Task<int> CountActiveByOrganisationAsync(
        Guid organisationId,
        IUnitOfWorkTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    Task<int> SoftDeleteAsync(
        Guid id,
        IUnitOfWorkTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    Task<int> SoftDeleteByOrganisationAsync(
        Guid organisationId,
        IUnitOfWorkTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetActiveOrganisationIdsWhereUserIsSoleMemberAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MembershipListItem>> ListActiveByOrganisationIdAsync(
        Guid organisationId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PendingMembershipListItem>> ListPendingByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<Membership?> GetActiveByUserAndOrganisationAsync(
        Guid userId,
        Guid organisationId,
        IUnitOfWorkTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    Task<int> UpdateStatusAsync(
        Guid id,
        MembershipStatus status,
        IUnitOfWorkTransaction? transaction = null,
        CancellationToken cancellationToken = default);
}
