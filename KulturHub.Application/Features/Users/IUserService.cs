using ErrorOr;
using KulturHub.Application.Features.Users.GetUser;

namespace KulturHub.Application.Features.Users;

public interface IUserService
{
    Task<ErrorOr<UserResponse>> GetUserAsync(GetUserInput input);
}
