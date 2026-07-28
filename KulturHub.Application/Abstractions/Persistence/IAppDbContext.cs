using KulturHub.Domain.Invitations;
using Microsoft.EntityFrameworkCore;

namespace KulturHub.Application.Abstractions.Persistence;

public interface IAppDbContext
{
    DbSet<Invitation> Invitations { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
