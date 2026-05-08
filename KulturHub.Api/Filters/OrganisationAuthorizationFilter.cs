using KulturHub.Api.Extensions;
using KulturHub.Domain.Interfaces;

namespace KulturHub.Api.Filters;

public static class OrganisationAuthorizationFilter
{
    public static IEndpointConventionBuilder RequireOrganisationMembership(
        this IEndpointConventionBuilder builder)
    {
        return builder.AddEndpointFilter(async (context, next) =>
        {
            var organisationIdValue = context.HttpContext.Request.RouteValues["organisationId"] as string;
            if (organisationIdValue is null || !Guid.TryParse(organisationIdValue, out var organisationId))
                return Results.BadRequest(new { error = "Invalid organisation id." });

            var user = context.HttpContext.User;
            var userId = user.GetUserId();

            var repo = context.HttpContext.RequestServices
                .GetRequiredService<IOrganisationRepository>();

            var isMember = await repo.IsMemberAsync(organisationId, userId);
            if (!isMember)
                return Results.Problem(
                    title: "Forbidden",
                    statusCode: StatusCodes.Status403Forbidden,
                    extensions: new Dictionary<string, object?> { ["code"] = "Organisation.Forbidden" });

            return await next(context);
        });
    }
}
