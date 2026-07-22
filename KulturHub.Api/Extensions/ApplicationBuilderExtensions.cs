using Microsoft.AspNetCore.Mvc;

namespace KulturHub.Api.Extensions;

public static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseKulturHubExceptionHandler(this IApplicationBuilder app)
    {
        return app.UseExceptionHandler(errorApp =>
        {
            errorApp.Run(async context =>
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/problem+json";

                var problemDetails = new ProblemDetails
                {
                    Title = "An unexpected error occurred",
                    Status = StatusCodes.Status500InternalServerError,
                    Instance = context.Request.Path,
                };

                await context.Response.WriteAsJsonAsync(problemDetails);
            });
        });
    }
}
