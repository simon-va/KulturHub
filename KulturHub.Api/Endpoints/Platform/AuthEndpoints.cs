using KulturHub.Api.Extensions;
using KulturHub.Api.Filters;
using KulturHub.Application.Features.Platform.Auth.SignIn;
using KulturHub.Application.Features.Public.Auth.SignUp;
using KulturHub.Application.Features.Public.Auth.ValidateInvitation;
using Microsoft.AspNetCore.Mvc;

namespace KulturHub.Api.Endpoints.Platform;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth")
            .WithTags("Auth")
            .WithGroupName("platform");

        group.MapPost("/signin", async (
            [FromBody] SignInRequest request,
            [FromServices] SignInHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(request, ct);

            return result.Match(
                response => Results.Json(response, statusCode: StatusCodes.Status200OK),
                errors => errors.ToResult());
        })
            .AddEndpointFilter<ValidationFilter<SignInRequest>>()
            .Produces<SignInResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithName("Auth_SignIn");

        group.MapPost("/signup", async (
            [FromBody] SignUpRequest request,
            [FromServices] SignUpHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(request, ct);

            return result.Match(
                response => Results.Json(response, statusCode: StatusCodes.Status201Created),
                errors => errors.ToResult());
        })
            .AddEndpointFilter<ValidationFilter<SignUpRequest>>()
            .Produces<SignUpResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithName("Auth_SignUp");

        group.MapPost("/validate-invitation", async (
            [FromBody] ValidateInvitationRequest request,
            [FromServices] ValidateInvitationHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(request, ct);

            return result.Match(
                _ => Results.Ok(),
                errors => errors.ToResult());
        })
            .AddEndpointFilter<ValidationFilter<ValidateInvitationRequest>>()
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithName("Auth_ValidateInvitation");

        return app;
    }
}