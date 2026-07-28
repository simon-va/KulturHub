using KulturHub.Domain.Invitations;
using KulturHub.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace KulturHub.Application.Abstractions.Persistence;

public interface IAppDbContext
{
    DbSet<Invitation> Invitations { get; }
    DbSet<User> Users { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}