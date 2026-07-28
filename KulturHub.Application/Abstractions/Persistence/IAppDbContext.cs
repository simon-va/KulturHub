using KulturHub.Domain.ChangeLogs;
using KulturHub.Domain.Invitations;
using KulturHub.Domain.Memberships;
using KulturHub.Domain.Organisations;
using KulturHub.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace KulturHub.Application.Abstractions.Persistence;

public interface IAppDbContext
{
    DbSet<Invitation> Invitations { get; }
    DbSet<User> Users { get; }
    DbSet<Organisation> Organisations { get; }
    DbSet<Membership> Memberships { get; }
    DbSet<ChangeLog> ChangeLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}