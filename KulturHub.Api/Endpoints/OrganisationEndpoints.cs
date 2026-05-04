using System.Security.Claims;
using KulturHub.Api.Extensions;
using KulturHub.Api.Requests;
using KulturHub.Application.Features.Organisations;
using KulturHub.Application.Features.Organisations.CreateOrganisation;
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
        }).RequireAuthorization();

        app.MapPost("/organisations", async (CreateOrganisationRequest req, ClaimsPrincipal user, IOrganisationService organisationService) =>
        {
            var userId = user.GetUserId();
            var result = await organisationService.CreateAsync(new CreateOrganisationInput(req.Name, userId));

            return result.Match(
                id => Results.Created($"/organisations/{id}", new { id }),
                errors => errors.ToResult());
        }).RequireAuthorization();

        app.MapPut("/organisations/{id:guid}", async (Guid id, UpdateOrganisationRequest req, ClaimsPrincipal user, IOrganisationService organisationService) =>
        {
            var userId = user.GetUserId();
            var result = await organisationService.UpdateAsync(id, new UpdateOrganisationInput(req.Name, userId));

            return result.Match(
                _ => Results.NoContent(),
                errors => errors.ToResult());
        }).RequireAuthorization();

        app.MapDelete("/organisations/{id:guid}", async (Guid id, ClaimsPrincipal user, IOrganisationService organisationService) =>
        {
            var userId = user.GetUserId();
            var result = await organisationService.DeleteAsync(id, userId);

            return result.Match(
                _ => Results.NoContent(),
                errors => errors.ToResult());
        }).RequireAuthorization();
    }
}
