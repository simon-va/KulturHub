using System.Security.Claims;
using KulturHub.Api.Extensions;
using KulturHub.Application.Features.Platform.Users.GetCurrentUser;
using Microsoft.AspNetCore.Mvc;

namespace KulturHub.Api.Endpoints.Platform;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/users")
            .WithTags("Users")
            .WithGroupName("platform")
            .RequireAuthorization();

        group.MapGet("/me", async (
            ClaimsPrincipal user,
            [FromServices] GetCurrentUserHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(user.GetUserId(), ct);

            return result.Match(
                response => Results.Json(response, statusCode: StatusCodes.Status200OK),
                errors => errors.ToResult());
        })
            .Produces<MeResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithName("Users_GetMe");

        return app;
    }
}
