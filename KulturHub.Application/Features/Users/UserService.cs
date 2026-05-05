using ErrorOr;
using KulturHub.Application.Errors;
using KulturHub.Application.Features.Users.GetUser;
using KulturHub.Domain.Interfaces;

namespace KulturHub.Application.Features.Users;

public class UserService(IUserRepository userRepository) : IUserService
{
    public async Task<ErrorOr<UserResponse>> GetUserAsync(GetUserInput input)
    {
        var user = await userRepository.GetByIdAsync(input.UserId);
        if (user is null)
            return UserErrors.NotFound(input.UserId);

        return new UserResponse(user.UserId, user.FirstName, user.LastName, user.IsAdmin);
    }
}
