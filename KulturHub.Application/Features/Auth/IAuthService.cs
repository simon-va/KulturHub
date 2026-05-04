using ErrorOr;
using KulturHub.Application.Features.Auth.SignUp;

namespace KulturHub.Application.Features.Auth;

public interface IAuthService
{
    Task<ErrorOr<AuthResponse>> SignUpAsync(SignUpInput input);
}
