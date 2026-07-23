using KulturHub.Api.Extensions;
using KulturHub.Api.Filters;
using KulturHub.Application.Features.Invitations.CreateInvitation;
using KulturHub.Application.Features.Invitations.DeleteInvitation;
using KulturHub.Application.Features.Invitations.ListInvitations;

namespace KulturHub.Api.Endpoints.Admin;

public static class InvitationEndpoints
{
    public static void MapInvitationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/invitations").WithTags("Invitations").WithGroupName("admin");

        group.MapPost("/", async (CreateInvitationHandler handler, CancellationToken ct) =>
        {
            var result = await handler.ExecuteAsync(ct);

            return result.Match(
                response => Results.Created($"/invitations/{response.Id}", response),
                errors => errors.ToResult());
        })
        .RequireAuthorization()
        .AddEndpointFilter<AdminAuthorizationFilter>()
        .Produces<CreateInvitationResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .WithName("Invitations_Create");

        group.MapGet("/", async (bool? includeUsed, bool? includeExpired, ListInvitationsHandler handler, CancellationToken ct) =>
        {
            var query = new ListInvitationsQuery(includeUsed ?? false, includeExpired ?? false);
            var result = await handler.ExecuteAsync(query, ct);

            return result.Match(response => Results.Ok(response), errors => errors.ToResult());
        })
        .RequireAuthorization()
        .AddEndpointFilter<AdminAuthorizationFilter>()
        .Produces<IReadOnlyList<InvitationListItem>>()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .WithName("Invitations_List");

        group.MapDelete("/{id:guid}", async (Guid id, DeleteInvitationHandler handler, CancellationToken ct) =>
        {
            var result = await handler.ExecuteAsync(id, ct);

            return result.Match(_ => Results.NoContent(), errors => errors.ToResult());
        })
        .RequireAuthorization()
        .AddEndpointFilter<AdminAuthorizationFilter>()
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .WithName("Invitations_Delete");
    }
}
