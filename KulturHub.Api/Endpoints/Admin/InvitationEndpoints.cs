using KulturHub.Api.Extensions;
using KulturHub.Api.Filters;
using KulturHub.Application.Features.Admin.Invitations.CreateInvitation;
using Microsoft.AspNetCore.Mvc;

namespace KulturHub.Api.Endpoints.Admin.Invitations;

public static class InvitationEndpoints
{
    public static IEndpointRouteBuilder MapInvitationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/invitations")
            .WithTags("Invitations")
            .WithGroupName("admin")
            .RequireAuthorization()
            .AddEndpointFilter<AdminAuthorizationFilter>();

        group.MapPost("/", async ([FromServices] CreateInvitationHandler handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(ct);

            return result.Match(
                response => Results.Json(response, statusCode: StatusCodes.Status201Created),
                errors => errors.ToResult());
        })
            .Produces<CreateInvitationResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithName("Invitations_Create");

        return app;
    }
}
