using KulturHub.Api.Extensions;
using KulturHub.Application.Ports;

namespace KulturHub.Api.Filters;

/// <summary>
/// Endpoint filter that allows access only to users with an <strong>accepted</strong> membership
/// for the organisation identified by the <c>organisationId</c> route parameter.
/// Pending or rejected memberships receive a 403 Forbidden response.
/// </summary>
public sealed class MembershipAuthorizationFilter(IMembershipReader membershipReader) : IEndpointFilter
{
    private const string RouteParameterName = "organisationId";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true)
            return Results.Unauthorized();

        var routeValue = context.HttpContext.Request.RouteValues[RouteParameterName];
        if (routeValue is null || !Guid.TryParse(routeValue.ToString(), out var organisationId))
            return Results.Problem(
                title: $"Route parameter '{RouteParameterName}' is missing or not a valid Guid.",
                statusCode: StatusCodes.Status500InternalServerError);

        var userId = user.GetUserId();
        var isMember = await membershipReader.IsMemberAsync(
            userId,
            organisationId,
            context.HttpContext.RequestAborted);

        if (!isMember)
            return Results.Problem(
                title: "Forbidden",
                statusCode: StatusCodes.Status403Forbidden,
                extensions: new Dictionary<string, object?> { ["code"] = "Organisation.NotMember" });

        return await next(context);
    }
}
