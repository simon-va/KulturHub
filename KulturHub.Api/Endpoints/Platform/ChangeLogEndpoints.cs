using KulturHub.Api.Extensions;
using KulturHub.Api.Filters;
using KulturHub.Application.Features.ChangeLogs.ListOrganisationChangeLogs;

namespace KulturHub.Api.Endpoints.Platform;

public static class ChangeLogEndpoints
{
    public static void MapChangeLogEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/organisations/{organisationId:guid}/change-logs", async (
                Guid organisationId,
                int? skip,
                int? take,
                ListOrganisationChangeLogsHandler handler,
                CancellationToken ct) =>
            {
                var query = new ListOrganisationChangeLogsQuery(organisationId, skip, take);
                var result = await handler.ExecuteAsync(query, ct);

                return result.Match(
                    response => Results.Ok(response),
                    errors => errors.ToResult());
            })
            .RequireAuthorization()
            .AddEndpointFilter<MembershipAuthorizationFilter>()
            .Produces<IReadOnlyList<ChangeLogListItem>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithName("ChangeLogs_ListByOrganisation")
            .WithTags("ChangeLogs")
            .WithGroupName("platform");
    }
}
