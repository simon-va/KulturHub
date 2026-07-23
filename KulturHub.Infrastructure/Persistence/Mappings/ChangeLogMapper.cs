using System.Text.Json;
using KulturHub.Domain.Entities;

namespace KulturHub.Infrastructure.Persistence.Mappings;

internal static class ChangeLogMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
    };

    internal static string SerializeData(IReadOnlyDictionary<string, object?> data) =>
        JsonSerializer.Serialize(data, JsonOptions);

    internal static IReadOnlyDictionary<string, object?> DeserializeData(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, object?>>(json, JsonOptions)
            ?? new Dictionary<string, object?>();

    internal static ChangeLog ToEntity(ChangeLogRow row)
    {
        var createdAt = DateTime.SpecifyKind(row.CreatedAt, DateTimeKind.Utc);
        return ChangeLog.Reconstitute(
            row.Id,
            row.OrganisationId,
            row.UserId,
            row.Message,
            DeserializeData(row.Data),
            createdAt);
    }

    internal sealed class ChangeLogRow
    {
        public Guid Id { get; init; }
        public Guid OrganisationId { get; init; }
        public Guid UserId { get; init; }
        public string Message { get; init; } = default!;
        public string Data { get; init; } = default!;
        public DateTime CreatedAt { get; init; }
    }
}
