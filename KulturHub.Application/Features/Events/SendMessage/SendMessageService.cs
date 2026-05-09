using System.Globalization;
using System.Text.Json;
using ErrorOr;
using KulturHub.Application.Errors;
using KulturHub.Application.Features.Events.GetConversation;
using KulturHub.Application.Ports;
using KulturHub.Domain.Entities;
using KulturHub.Domain.Enums;
using KulturHub.Domain.Interfaces;

namespace KulturHub.Application.Features.Events.SendMessage;

public class SendMessageService(
    IEventRepository eventRepository,
    IMessageRepository messageRepository,
    IEventCategoryRepository eventCategoryRepository,
    IAiChatService aiChatService) : ISendMessageService
{
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    private const string BaseSystemPrompt = """
        Du bist ein freundlicher Assistent, der Vereinsvertretern hilft, ihre Veranstaltung für den Kulturkalender zu erfassen.
        Frage nach diesen sechs Feldern: Titel der Veranstaltung, Adresse/Ort, Beschreibung, Beginn (Datum + Uhrzeit), Ende (Datum + Uhrzeit) und Kategorie.
        Frage immer nur nach einer fehlenden Information auf einmal. Antworte ausschließlich auf Deutsch.
        Setze "status" auf "ready", wenn alle sechs Felder vollständig und plausibel sind (Ende muss nach Beginn liegen).
        Gib Beginn und Ende im ISO-8601-Format zurück, z.B. "2025-07-12T18:00:00".
        Alle Uhrzeiten sind in der deutschen Zeitzone (Europe/Berlin).
        Felder, die noch nicht bekannt sind, lasse im JSON weg.
        Wähle die passende Kategorie anhand von Titel und Beschreibung aus der bereitgestellten Liste. Bist du dir unsicher, frage den Nutzer zur Bestätigung nach.
        Die category_id muss exakt eine der bereitgestellten IDs sein.
        Wenn alle benötigten Felder mit Daten gefüllt sind, kannst du dem Nutzer Bescheid geben, dass er die Veranstaltung veröffentlichen kann.
        Du musst in JEDER Antwort einen Wert für "reply" zurückgeben. Das ist deine direkte, freundliche Antwort an den Nutzer.
        """;

    private const string JsonSchema = """
        {
          "type": "object",
          "properties": {
            "title":        { "type": "string" },
            "address":      { "type": "string" },
            "description":  { "type": "string" },
            "start_time":   { "type": "string" },
            "end_time":     { "type": "string" },
            "category_id":  { "type": "string", "format": "uuid" },
            "status":       { "type": "string", "enum": ["incomplete", "ready"] },
            "reply":        { "type": "string" }
          },
          "required": ["status", "reply"],
          "additionalProperties": false
        }
        """;

    private string BuildSystemPrompt(Event @event, IEnumerable<EventCategory> categories)
    {
        var context = new System.Text.StringBuilder();
        context.AppendLine(BaseSystemPrompt);
        context.AppendLine();
        context.AppendLine("Aktueller Stand der Veranstaltung:");
        context.AppendLine($"- Titel: {(!string.IsNullOrWhiteSpace(@event.Title) ? @event.Title : "(noch leer)")}");
        context.AppendLine($"- Adresse: {(!string.IsNullOrWhiteSpace(@event.Address) ? @event.Address : "(noch leer)")}");
        context.AppendLine($"- Beschreibung: {(!string.IsNullOrWhiteSpace(@event.Description) ? @event.Description : "(noch leer)")}");
        context.AppendLine($"- Beginn: {(@event.StartTime.HasValue ? ToBerlinIsoString(@event.StartTime.Value) : "(noch leer)")}");
        context.AppendLine($"- Ende: {(@event.EndTime.HasValue ? ToBerlinIsoString(@event.EndTime.Value) : "(noch leer)")}");
        context.AppendLine($"- Kategorie: {(@event.EventCategoryId.HasValue ? categories.FirstOrDefault(c => c.Id == @event.EventCategoryId.Value)?.Name ?? "(unbekannt)" : "(noch leer)")}");
        context.AppendLine();
        context.AppendLine("Verfügbare Kategorien (ID → Name):");
        foreach (var category in categories)
        {
            context.AppendLine($"- {category.Id} → {category.Name}");
        }
        context.AppendLine();
        context.AppendLine("Gib in deiner Antwort immer alle bereits bekannten Felder zurück, zusammen mit neuen oder geänderten Informationen.");
        return context.ToString();
    }

    public async Task<ErrorOr<SendMessageResponse>> SendMessageAsync(
        SendMessageInput input, CancellationToken cancellationToken = default)
    {
        var @event = await eventRepository.GetByIdAsync(input.EventId, input.OrganisationId);
        if (@event is null) return EventErrors.NotFound(input.EventId);
        if (@event.ConversationId is null) return EventErrors.NoConversation(input.EventId);

        var categories = await eventCategoryRepository.GetAllAsync();
        var categoryIds = categories.Select(c => c.Id).ToHashSet();

        var allMessages = await messageRepository.GetByConversationIdAsync(@event.ConversationId.Value);
        var history = allMessages
            .Select(m => new AiMessage(
                Role: m.Role == MessageRole.User ? "user" : "assistant",
                Content: m.Content))
            .Append(new AiMessage("user", input.Content))
            .ToList();

        var jsonReply = await aiChatService.GetStructuredReplyAsync(
            BuildSystemPrompt(@event, categories), JsonSchema, history, cancellationToken);

        EventAiResponse aiResponse;
        try
        {
            aiResponse = JsonSerializer.Deserialize<EventAiResponse>(jsonReply)
                ?? throw new InvalidOperationException("AI returned unparseable JSON.");
        }
        catch (JsonException)
        {
            return EventErrors.AiParseError();
        }

        DateTime? parsedStart = null;
        DateTime? parsedEnd = null;
        int? parsedCategoryId = null;

        if (aiResponse.StartTime is not null)
        {
            if (!TryParseAiDateTime(aiResponse.StartTime, out var start))
                return EventErrors.AiParseError();
            parsedStart = start;
        }

        if (aiResponse.EndTime is not null)
        {
            if (!TryParseAiDateTime(aiResponse.EndTime, out var end))
                return EventErrors.AiParseError();
            parsedEnd = end;
        }

        if (!string.IsNullOrWhiteSpace(aiResponse.CategoryId))
        {
            if (!int.TryParse(aiResponse.CategoryId, out var categoryId) || !categoryIds.Contains(categoryId))
                return EventErrors.AiParseError();
            parsedCategoryId = categoryId;
        }

        if (string.IsNullOrWhiteSpace(aiResponse.Reply))
            return EventErrors.AiParseError();

        if (aiResponse.Status == "ready" && parsedCategoryId is null)
            return EventErrors.IncompleteAiResponse();

        var newStatus = aiResponse.Status == "ready" ? EventStatus.ReadyToPublish : EventStatus.Draft;

        @event.UpdateDetails(
            title: aiResponse.Title,
            address: aiResponse.Address,
            description: aiResponse.Description,
            startTime: parsedStart,
            endTime: parsedEnd,
            newStatus: newStatus,
            eventCategoryId: parsedCategoryId);

        var userMessage = Message.Create(@event.ConversationId.Value, MessageRole.User, input.Content);
        var botMessage = Message.Create(@event.ConversationId.Value, MessageRole.Assistant, aiResponse.Reply);

        await messageRepository.CreateAsync(userMessage);
        try
        {
            await eventRepository.UpdateDraftAsync(@event);
        }
        catch (Domain.Exceptions.ConcurrencyException)
        {
            return EventErrors.ConcurrencyConflict();
        }
        @event.IncrementVersion();
        await messageRepository.CreateAsync(botMessage);

        return new SendMessageResponse(
            new MessageResponse(userMessage.Id, userMessage.Role, userMessage.Content, userMessage.CreatedAt),
            new MessageResponse(botMessage.Id, botMessage.Role, botMessage.Content, botMessage.CreatedAt));
    }

    private static bool TryParseAiDateTime(string? value, out DateTime result)
    {
        if (value is null)
        {
            result = default;
            return false;
        }

        if (DateTime.TryParseExact(value, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var roundtrip))
        {
            result = roundtrip.Kind switch
            {
                DateTimeKind.Utc => roundtrip,
                DateTimeKind.Local => roundtrip.ToUniversalTime(),
                _ => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(roundtrip, DateTimeKind.Unspecified), Berlin),
            };
            return true;
        }

        string[] fallbackFormats = ["yyyy-MM-ddTHH:mm:ssK"];
        if (DateTime.TryParseExact(value, fallbackFormats, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var fallback))
        {
            result = fallback.Kind switch
            {
                DateTimeKind.Utc => fallback,
                DateTimeKind.Local => fallback.ToUniversalTime(),
                _ => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(fallback, DateTimeKind.Unspecified), Berlin),
            };
            return true;
        }

        result = default;
        return false;
    }

    private static string ToBerlinIsoString(DateTime utc)
    {
        DateTime local = TimeZoneInfo.ConvertTimeFromUtc(utc, Berlin);
        return local.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
    }
}
