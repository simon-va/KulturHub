using KulturHub.Domain.Entities;

namespace KulturHub.Domain.Interfaces;

public interface IPostRepository
{
    Task<Post> CreateAsync(Post post);
    Task UpdateAsync(Post post);
    Task<Post?> GetByIdAsync(Guid id);
}
