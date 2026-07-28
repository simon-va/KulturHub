using KulturHub.Application.Abstractions.Persistence;
using KulturHub.Application.Ports;
using KulturHub.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace KulturHub.Infrastructure.Users;

public sealed class UserReader(IAppDbContext db) : IUserReader
{
    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
}