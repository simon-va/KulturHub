using System.Security.Claims;
using KulturHub.Api.Extensions;
using KulturHub.Api.Requests;
using KulturHub.Api.Responses;
using KulturHub.Application.Features.Organisations;
using KulturHub.Application.Features.Organisations.CreateOrganisation;
using KulturHub.Application.Features.Organisations.GetOrganisations;
using KulturHub.Application.Features.Organisations.UpdateOrganisation;

namespace KulturHub.Api.Endpoints;

public static class OrganisationEndpoints
{
    public static void MapOrganisationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/organisations", async (ClaimsPrincipal user, IOrganisationService organisationService) =>
        {
            var userId = user.GetUserId();
            var organisations = await organisationService.GetByUserIdAsync(userId);
            return Results.Ok(organisations);
        })
        .RequireAuthorization()
        .Produces<IEnumerable<OrganisationResponse>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .WithName("Organisation_GetOrganisations")
        .WithTags("Organisation");

        app.MapPost("/organisations", async (CreateOrganisationRequest req, ClaimsPrincipal user, IOrganisationService organisationService) =>
        {
            var userId = user.GetUserId();
            var result = await organisationService.CreateAsync(new CreateOrganisationInput(req.Name, userId));

            return result.Match(
                id => Results.Created($"/organisations/{id}", new CreatedResponse(id)),
                errors => errors.ToResult());
        })
        .RequireAuthorization()
        .Produces<CreatedResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .WithName("Organisation_CreateOrganisation")
        .WithTags("Organisation");

        app.MapPut("/organisations/{id:guid}", async (Guid id, UpdateOrganisationRequest req, ClaimsPrincipal user, IOrganisationService organisationService) =>
        {
            var userId = user.GetUserId();
            var result = await organisationService.UpdateAsync(id, new UpdateOrganisationInput(req.Name, userId));

            return result.Match(
                _ => Results.NoContent(),
                errors => errors.ToResult());
        })
        .RequireAuthorization()
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithName("Organisation_UpdateOrganisation")
        .WithTags("Organisation");

        app.MapDelete("/organisations/{id:guid}", async (Guid id, ClaimsPrincipal user, IOrganisationService organisationService) =>
        {
            var userId = user.GetUserId();
            var result = await organisationService.DeleteAsync(id, userId);

            return result.Match(
                _ => Results.NoContent(),
                errors => errors.ToResult());
        })
        .RequireAuthorization()
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithName("Organisation_DeleteOrganisation")
        .WithTags("Organisation");
    }
}
