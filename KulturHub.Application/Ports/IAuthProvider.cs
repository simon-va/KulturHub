using ErrorOr;

namespace KulturHub.Application.Ports;

public record AuthProviderSession(string AccessToken, string RefreshToken, Guid UserId);

public interface IAuthProvider
{
    Task<ErrorOr<AuthProviderSession>> SignUpAsync(string email, string password);
}
