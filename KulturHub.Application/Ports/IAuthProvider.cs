using ErrorOr;

namespace KulturHub.Application.Ports;

public interface IAuthProvider
{
    Task<ErrorOr<AuthProviderSession>> SignUpAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default);
}