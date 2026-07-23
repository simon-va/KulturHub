using System.Security.Claims;
using KulturHub.Api.Extensions;
using KulturHub.Api.Filters;
using KulturHub.Api.Requests;
using KulturHub.Application.Features.Organisations.CreateOrganisation;
using KulturHub.Application.Features.Organisations.ListUserOrganisations;
using KulturHub.Application.Features.Organisations.UpdateOrganisation;

namespace KulturHub.Api.Endpoints.Platform;

public static class OrganisationEndpoints
{
    public static void MapOrganisationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/organisations").WithTags("Organisations").WithGroupName("platform");

        group.MapPost("/", async (
            CreateOrganisationRequest request,
            ClaimsPrincipal user,
            CreateOrganisationHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.ExecuteAsync(
                new CreateOrganisationInput(request.Name, user.GetUserId()),
                ct);

            return result.Match(
                response => Results.Created($"/organisations/{response.Id}", response),
                errors => errors.ToResult());
        })
        .RequireAuthorization()
        .Produces<CreateOrganisationResponse>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithName("Organisations_Create");

        group.MapGet("/mine", async (
            ClaimsPrincipal user,
            ListUserOrganisationsHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.ExecuteAsync(user.GetUserId(), ct);

            return result.Match(
                response => Results.Ok(response),
                errors => errors.ToResult());
        })
        .RequireAuthorization()
        .Produces<IReadOnlyList<OrganisationSummary>>()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .WithName("Organisations_ListMine");

        group.MapPut("/{organisationId:guid}", async (
            Guid organisationId,
            UpdateOrganisationRequest request,
            ClaimsPrincipal user,
            UpdateOrganisationHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.ExecuteAsync(
                new UpdateOrganisationInput(request.Name, user.GetUserId(), organisationId),
                ct);

            return result.Match(
                response => Results.Ok(response),
                errors => errors.ToResult());
        })
        .RequireAuthorization()
        .AddEndpointFilter<MembershipAuthorizationFilter>()
        .Produces<UpdateOrganisationResponse>(StatusCodes.Status200OK)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithName("Organisations_Update");
    }
}
