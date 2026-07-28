using System.Security.Claims;
using KulturHub.Api.Extensions;
using KulturHub.Api.Filters;
using KulturHub.Application.Features.Platform.Organisations.CreateOrganisation;
using Microsoft.AspNetCore.Mvc;

namespace KulturHub.Api.Endpoints.Platform.Organisations;

public static class OrganisationEndpoints
{
    public static IEndpointRouteBuilder MapOrganisationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/organisations")
            .WithTags("Organisations")
            .WithGroupName("platform")
            .RequireAuthorization();

        group.MapPost("/", async (
            [FromBody] CreateOrganisationRequest request,
            ClaimsPrincipal user,
            [FromServices] CreateOrganisationHandler handler,
            CancellationToken ct) =>
        {
            var command = new CreateOrganisationCommand(user.GetUserId(), request.Name);

            var result = await handler.HandleAsync(command, ct);

            return result.Match(
                response => Results.Json(response, statusCode: StatusCodes.Status201Created),
                errors => errors.ToResult());
        })
            .AddEndpointFilter<ValidationFilter<CreateOrganisationRequest>>()
            .Produces<CreateOrganisationResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithName("Organisations_Create");

        return app;
    }
}
