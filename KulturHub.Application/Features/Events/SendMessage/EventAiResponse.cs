using System.Text.Json.Serialization;

namespace KulturHub.Application.Features.Events.SendMessage;

internal record EventAiResponse(
    [property: JsonPropertyName("title")]        string? Title,
    [property: JsonPropertyName("address")]      string? Address,
    [property: JsonPropertyName("description")]  string? Description,
    [property: JsonPropertyName("start_time")]   string? StartTime,
    [property: JsonPropertyName("end_time")]     string? EndTime,
    [property: JsonPropertyName("category_id")]  string? CategoryId,
    [property: JsonPropertyName("status")]       string? Status,
    [property: JsonPropertyName("reply")]        string? Reply);
