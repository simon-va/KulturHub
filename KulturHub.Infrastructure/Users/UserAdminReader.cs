using KulturHub.Application.Abstractions.Persistence;
using KulturHub.Application.Ports;
using KulturHub.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace KulturHub.Infrastructure.Users;

public sealed class UserAdminReader(IAppDbContext db) : IUserAdminReader
{
    public Task<bool> IsAdminAsync(Guid userId, CancellationToken cancellationToken = default) =>
        db.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == UserId.From(userId) && u.IsAdmin, cancellationToken);
}