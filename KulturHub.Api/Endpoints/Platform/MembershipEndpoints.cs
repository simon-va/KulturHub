using KulturHub.Api.Extensions;
using KulturHub.Api.Filters;
using KulturHub.Application.Features.Platform.Memberships.ListMemberships;
using Microsoft.AspNetCore.Mvc;

namespace KulturHub.Api.Endpoints.Platform;

public static class MembershipEndpoints
{
    public static IEndpointRouteBuilder MapMembershipEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/organisations/{organisationId:guid}/memberships")
            .WithTags("Memberships")
            .WithGroupName("platform")
            .RequireAuthorization()
            .AddEndpointFilter<MembershipAuthorizationFilter>();

        group.MapGet("/", async (
            Guid organisationId,
            [FromServices] ListMembershipsHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(organisationId, ct);

            return result.Match(
                response => Results.Json(response, statusCode: StatusCodes.Status200OK),
                errors => errors.ToResult());
        })
            .Produces<IReadOnlyList<MembershipResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithName("Memberships_ListByOrganisation");

        return app;
    }
}