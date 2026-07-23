using System.Text.Json.Serialization;

namespace KulturHub.Api.Extensions;

public static class JsonServiceCollectionExtensions
{
    public static IServiceCollection AddKulturHubJson(this IServiceCollection services)
    {
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });
        return services;
    }
}
