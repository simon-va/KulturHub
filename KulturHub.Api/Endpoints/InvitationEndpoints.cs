using System.Security.Claims;
using KulturHub.Api.Extensions;
using KulturHub.Application.Features.Invitations.CreateInvitation;

namespace KulturHub.Api.Endpoints;

public static class InvitationEndpoints
{
    public static void MapInvitationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/admin/invitations", async (
            ClaimsPrincipal user,
            ICreateInvitationService createInvitationService) =>
        {
            var userId = user.GetUserId();
            var result = await createInvitationService.CreateAsync(new CreateInvitationInput(userId));

            return result.Match(
                response => Results.Created($"/admin/invitations/{response.Id}", response),
                errors => errors.ToResult());
        })
        .RequireAuthorization()
        .Produces<CreateInvitationResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .WithName("Invitation_CreateInvitation")
        .WithTags("Invitation");
    }
}
