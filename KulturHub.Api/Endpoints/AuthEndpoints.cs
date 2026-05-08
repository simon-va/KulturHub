using KulturHub.Api.Extensions;
using KulturHub.Api.Requests;
using KulturHub.Application.Features.Auth;
using KulturHub.Application.Features.Auth.SignUp;

namespace KulturHub.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/signup", async (SignUpRequest req, IAuthService authService) =>
        {
            var result = await authService.SignUpAsync(
                new SignUpInput(req.FirstName, req.LastName, req.Email, req.Password, req.InvitationCode));

            return result.Match(
                response => Results.Ok(response),
                errors => errors.ToResult());
        })
        .Produces<AuthResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .WithName("Auth_SignUp")
        .WithTags("Auth");
    }
}
