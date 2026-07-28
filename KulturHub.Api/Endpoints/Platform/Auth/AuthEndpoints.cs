using KulturHub.Api.Extensions;
using KulturHub.Api.Filters;
using KulturHub.Application.Features.Public.Auth.SignUp;
using Microsoft.AspNetCore.Mvc;

namespace KulturHub.Api.Endpoints.Platform.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth")
            .WithTags("Auth")
            .WithGroupName("platform");

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

        return app;
    }
}