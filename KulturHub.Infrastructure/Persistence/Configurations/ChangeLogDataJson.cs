using System.Text.Json;
using KulturHub.Domain.ChangeLogs;

namespace KulturHub.Infrastructure.Persistence.Configurations;

internal static class ChangeLogDataJson
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
    };

    public static string Serialize(IReadOnlyDictionary<string, string?> data) =>
        JsonSerializer.Serialize(data, SerializerOptions);

    public static IReadOnlyDictionary<string, string?> Deserialize(string json)
    {
        var result = JsonSerializer.Deserialize<Dictionary<string, string?>>(json, SerializerOptions);
        return result ?? new Dictionary<string, string?>();
    }
}
