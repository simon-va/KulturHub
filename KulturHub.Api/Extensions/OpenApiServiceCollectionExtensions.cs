using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace KulturHub.Api.Extensions;

public static class OpenApiServiceCollectionExtensions
{
    public static IServiceCollection AddKulturHubOpenApi(this IServiceCollection services)
    {
        AddDocument(services, "public", "KulturHub Public API", "public");
        AddDocument(services, "platform", "KulturHub Platform API", "platform");
        AddDocument(services, "admin", "KulturHub Admin API", "admin");
        return services;
    }

    private static void AddDocument(IServiceCollection services, string name, string title, string groupName)
    {
        services.AddOpenApi(name, options =>
        {
            options.ShouldInclude = description => description.GroupName == groupName;

            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new OpenApiInfo { Title = title, Version = "v1" };
                return Task.CompletedTask;
            });

            options.AddDocumentTransformer(new BearerSecurityDocumentTransformer());
        });
    }

    private sealed class BearerSecurityDocumentTransformer : IOpenApiDocumentTransformer
    {
        public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
        {
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT"
            };
            return Task.CompletedTask;
        }
    }
}
