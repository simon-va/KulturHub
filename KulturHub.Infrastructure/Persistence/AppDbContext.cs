using KulturHub.Application.Abstractions.Persistence;
using KulturHub.Domain.ChangeLogs;
using KulturHub.Domain.Invitations;
using KulturHub.Domain.Memberships;
using KulturHub.Domain.Organisations;
using KulturHub.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace KulturHub.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Organisation> Organisations => Set<Organisation>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<ChangeLog> ChangeLogs => Set<ChangeLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}