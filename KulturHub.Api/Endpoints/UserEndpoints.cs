using System.Security.Claims;
using KulturHub.Api.Extensions;
using KulturHub.Application.Features.Users;
using KulturHub.Application.Features.Users.GetUser;

namespace KulturHub.Api.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/users/{userId:guid}", async (Guid userId, ClaimsPrincipal user, IUserService userService) =>
        {
            var result = await userService.GetUserAsync(new GetUserInput(userId));

            return result.Match(
                userResponse => Results.Ok(userResponse),
                errors => errors.ToResult());
        })
        .RequireAuthorization()
        .Produces<UserResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithName("User_GetUser")
        .WithTags("User");
    }
}
