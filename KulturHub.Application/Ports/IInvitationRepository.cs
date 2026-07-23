using ErrorOr;
using KulturHub.Application.Features.Invitations.ListInvitations;
using KulturHub.Domain.Entities;

namespace KulturHub.Application.Ports;

public interface IInvitationRepository
{
    Task<Invitation?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Invitation?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<IReadOnlyList<InvitationListItem>> ListAsync(InvitationFilter filter, CancellationToken ct = default);
    Task InsertAsync(Invitation invitation, IUnitOfWorkTransaction? transaction = null, CancellationToken ct = default);
    Task<ErrorOr<Success>> MarkAsUsedAsync(Guid id, Guid userId, IUnitOfWorkTransaction? transaction = null, CancellationToken ct = default);
    Task<int> DeleteAsync(Guid id, IUnitOfWorkTransaction? transaction = null, CancellationToken ct = default);
}
