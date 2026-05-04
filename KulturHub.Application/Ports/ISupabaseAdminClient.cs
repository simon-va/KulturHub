namespace KulturHub.Application.Ports;

public interface ISupabaseAdminClient
{
    Task<bool> DeleteUserAsync(Guid userId);
}
