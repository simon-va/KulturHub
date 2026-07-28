using System.Security.Claims;
using KulturHub.Api.Extensions;
using KulturHub.Api.Filters;
using KulturHub.Application.Features.Platform.Memberships.InviteMembership;
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

        group.MapPost("/invite", async (
            Guid organisationId,
            [FromBody] InviteMembershipRequest request,
            ClaimsPrincipal user,
            [FromServices] InviteMembershipHandler handler,
            CancellationToken ct) =>
        {
            var command = new InviteMembershipCommand(
                organisationId,
                user.GetUserId(),
                request.Email);

            var result = await handler.HandleAsync(command, ct);

            return result.Match(
                response => Results.Json(response, statusCode: StatusCodes.Status201Created),
                errors => errors.ToResult());
        })
            .AddEndpointFilter<ValidationFilter<InviteMembershipRequest>>()
            .Produces<InviteMembershipResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithName("Memberships_Invite");

        return app;
    }
}