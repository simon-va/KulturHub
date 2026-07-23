using System.Security.Claims;
using KulturHub.Api.Extensions;
using KulturHub.Api.Filters;
using KulturHub.Api.Requests;
using KulturHub.Application.Features.Memberships.DeleteOrganisationMembership;
using KulturHub.Application.Features.Memberships.InviteMember;
using KulturHub.Application.Features.Memberships.ListMyPendingMemberships;
using KulturHub.Application.Features.Memberships.ListOrganisationMemberships;
using KulturHub.Application.Features.Memberships.RespondToMembership;

namespace KulturHub.Api.Endpoints.Platform;

public static class MembershipEndpoints
{
    public static void MapMembershipEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/organisations/{organisationId:guid}/memberships")
            .WithTags("Memberships")
            .WithGroupName("platform");

        group.MapGet("/", async (
                Guid organisationId,
                ListOrganisationMembershipsHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler.ExecuteAsync(organisationId, ct);

                return result.Match(
                    response => Results.Ok(response),
                    errors => errors.ToResult());
            })
            .RequireAuthorization()
            .AddEndpointFilter<MembershipAuthorizationFilter>()
            .Produces<IReadOnlyList<MembershipListItem>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithName("Memberships_ListByOrganisation");

        group.MapPost("/invites", async (
                Guid organisationId,
                InviteMemberRequest request,
                ClaimsPrincipal user,
                InviteMemberHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler.ExecuteAsync(
                    new InviteMemberInput(organisationId, request.Email, user.GetUserId()),
                    ct);

                return result.Match(
                    response => Results.Created(
                        $"/organisations/{organisationId}/memberships/{response.MembershipId}",
                        response),
                    errors => errors.ToResult());
            })
            .RequireAuthorization()
            .AddEndpointFilter<MembershipAuthorizationFilter>()
            .Produces<InvitedMember>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName("Memberships_Invite");

        group.MapDelete("/{membershipId:guid}", async (
                Guid organisationId,
                Guid membershipId,
                ClaimsPrincipal user,
                DeleteOrganisationMembershipHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler.ExecuteAsync(
                    new DeleteOrganisationMembershipInput(organisationId, membershipId, user.GetUserId()),
                    ct);

                return result.Match(
                    _ => Results.NoContent(),
                    errors => errors.ToResult());
            })
            .RequireAuthorization()
            .AddEndpointFilter<MembershipAuthorizationFilter>()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName("Memberships_Delete");

        app.MapGet("/memberships/me/pending", async (
                ClaimsPrincipal user,
                ListMyPendingMembershipsHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler.ExecuteAsync(user.GetUserId(), ct);

                return result.Match(
                    response => Results.Ok(response),
                    errors => errors.ToResult());
            })
            .RequireAuthorization()
            .WithTags("Memberships")
            .WithGroupName("platform")
            .Produces<IReadOnlyList<PendingMembershipListItem>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithName("Memberships_ListMyPending");

        app.MapPatch("/memberships/me/{membershipId:guid}/status", async (
                Guid membershipId,
                RespondMembershipRequest request,
                ClaimsPrincipal user,
                RespondToMembershipHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler.ExecuteAsync(
                    new RespondToMembershipInput(membershipId, user.GetUserId(), request.Decision),
                    ct);

                return result.Match(
                    _ => Results.NoContent(),
                    errors => errors.ToResult());
            })
            .RequireAuthorization()
            .WithTags("Memberships")
            .WithGroupName("platform")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName("Memberships_Respond");
    }
}
