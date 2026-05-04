namespace KulturHub.Domain.Interfaces;

public interface ISupabaseAdminClient
{
    Task<bool> DeleteUserAsync(Guid userId);
}
