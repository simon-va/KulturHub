using System.Security.Claims;
using KulturHub.Api.Extensions;
using KulturHub.Api.Requests;
using KulturHub.Application.Features.Auth.DeleteAccount;
using KulturHub.Application.Features.Auth.SignIn;
using KulturHub.Application.Features.Auth.SignUp;
using KulturHub.Application.Features.Auth.ValidateInvitation;

namespace KulturHub.Api.Endpoints.Platform;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/signup", async (SignUpRequest req, SignUpHandler handler, CancellationToken ct) =>
        {
            var result = await handler.ExecuteAsync(
                new SignUpInput(req.FirstName, req.LastName, req.Email, req.Password, req.InvitationCode), ct);

            return result.Match(
                response => Results.Created("/auth/me", response),
                errors => errors.ToResult());
        })
        .Produces<SignUpResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .WithName("Auth_SignUp")
        .WithTags("Auth")
        .WithGroupName("platform");

        app.MapPost("/auth/validate-invitation", async (
            ValidateInvitationRequest req,
            ValidateInvitationHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.ExecuteAsync(
                new ValidateInvitationInput(req.InvitationCode), ct);

            return result.Match(
                _ => Results.Ok(),
                errors => errors.ToResult());
        })
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .WithName("Auth_ValidateInvitation")
        .WithTags("Auth")
        .WithGroupName("platform");

        app.MapPost("/auth/login", async (SignInRequest req, SignInHandler handler, CancellationToken ct) =>
        {
            var result = await handler.ExecuteAsync(
                new SignInInput(req.Email, req.Password), ct);

            return result.Match(
                response => Results.Ok(response),
                errors => errors.ToResult());
        })
        .Produces<SignInResponse>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithName("Auth_SignIn")
        .WithTags("Auth")
        .WithGroupName("platform");

        app.MapDelete("/auth/me", async (
            ClaimsPrincipal user,
            DeleteAccountHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.ExecuteAsync(user.GetUserId(), ct);
            return result.Match(
                _ => Results.NoContent(),
                errors => errors.ToResult());
        })
        .RequireAuthorization()
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithName("Auth_DeleteMe")
        .WithTags("Auth")
        .WithGroupName("platform");
    }
}
