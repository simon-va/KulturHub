namespace KulturHub.Api.Endpoints.Public;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
            .WithName("Public_Health")
            .WithTags("Public")
            .WithGroupName("public")
            .Produces(StatusCodes.Status200OK);
    }
}
