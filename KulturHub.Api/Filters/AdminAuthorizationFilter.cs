using KulturHub.Api.Extensions;
using KulturHub.Application.Ports;

namespace KulturHub.Api.Filters;

public sealed class AdminAuthorizationFilter(IUserAdminReader userAdminReader) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true)
            return Results.Unauthorized();

        var userId = user.GetUserId();
        var isAdmin = await userAdminReader.IsAdminAsync(
            userId, context.HttpContext.RequestAborted);

        if (!isAdmin)
            return Results.Problem(
                title: "Forbidden",
                statusCode: StatusCodes.Status403Forbidden,
                extensions: new Dictionary<string, object?> { ["code"] = "User.NotAdmin" });

        return await next(context);
    }
}