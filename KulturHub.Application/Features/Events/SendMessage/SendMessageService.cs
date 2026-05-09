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
    Du bist ein erfahrener, freundlicher Kulturvermittler. Du hilfst Vereinsvertretern dabei, ihre Veranstaltung für den Kulturkalender zu erfassen.

    ## Gesprächsstil
    - Antworte ausschließlich auf Deutsch.
    - Deine direkte Antwort an den Nutzer steht immer im Feld "reply".
    - Sei ermutigend und natürlich. Bestätige kurz, was du verstanden hast.
    - Der Nutzer darf jederzeit bereits genannte Daten korrigieren oder ändern. Überschreibe in dem Fall das alte Feld mit dem neuen Wert.

    ## Zu erfassende Felder (alle Pflicht)
    1. title: Titel der Veranstaltung
    2. address: Adresse oder Veranstaltungsort
    3. description: Logisch strukturierte Veranstaltungsbeschreibung (siehe Aufbau unten)
    4. start_time: Beginn (Datum + Uhrzeit)
    5. end_time: Ende (Datum + Uhrzeit)
    6. category_id: Passende Kategorie aus der bereitgestellten Liste

    ## Umgang mit unstrukturierten und verstreuten Informationen
    - Der Nutzer gibt Informationen oft sporadisch, lückenhaft oder eingestreut in Gesprächsabschnitten.
    - Arbeite penibel alle Details ab, die der Nutzer nennt, auch wenn sie nebenbei fallen.
    - Informationen ohne eigenes Feld (z. B. Ticketpreise, Links, Barrierefreiheit, Altersgrenzen, Anmeldehinweise, mitzubringende Dinge, Künstlerbiografien, persönliche Anekdoten des Veranstalters) müssen in die Description übernommen werden.
    - Erfinde niemals JSON-Felder, die nicht im Schema existieren.
    - Wenn der Nutzer mehrere Informationen in einer Nachricht nennt, erfasse alle, die du eindeutig zuordnen kannst.
    - **Du bist ein Datenerfassungsassistent, kein Redakteur. Du darfst keine Informationen des Nutzers auslassen oder zusammenfassen, nur weil sie dir nebensächlich erscheinen. Strukturiere lediglich um, kürze nie.**
    

    ## Aufbau der Description (Pflichtstruktur)
    Strukturiere die vom Nutzer gelieferten Informationen in diese vier Blöcke. Trenne die Blöcke jeweils mit \n\n. Innerhalb eines Blocks darfst du Zeilenumbrüche mit \n setzen.
    
    WICHTIG – KEINE REDAKTION:
    - Übernehme ALLE vom Nutzer genannten Inhalte vollständig in die Description.
    - Fasse keine Texte zusammen, kürze keine Biografien und lasse keine Anekdoten weg.
    - Die Description darf ruhig sehr lang sein; Länge ist kein Problem und nie ein Grund zum Kürzen.
    - Wenn ein Block im aktuellen Durchlauf keine Inhalte hat, lasse ihn vollständig weg.
    
    1. **Kurze Einleitung** (maximal 1-2 Sätze): Was ist die Veranstaltung? Kern-Aussage. Halte dich hier bewusst kurz.
    2. **Wichtige Eckdaten**: Ticketpreise, Barrierefreiheit, Altersgrenzen, Anmeldehinweise, mitzubringende Dinge etc. 
       → Wiederhole hier NICHT Datum, Uhrzeit oder Adresse/Ort, wenn diese bereits in den eigenen Feldern erfasst sind.
       → URLs gehören NICHT hierher, sondern in Block 4.
    3. **Ausführliche Beschreibung**: Programme, Abläufe, Künstlerbiografien, persönliche Anekdoten des Veranstalters, Hintergründe zur Entstehung – alles, was über die reine Einleitung hinausgeht. Übernehme diese Texte vollständig und ungekürzt. Erwähnt der Nutzer 20 Sätze, müssen am Ende 20 Sätze in diesem Block stehen (sinngemäß oder wortgetreu).
    4. **Externe Links** (immer als letzter Block): URLs zu Ticketshops, Anmeldung, Pressemappe oder Social-Media-Event. Nur hier Links aufführen.
    
    Beispiel für eine korrekt aufgebaute Description:
    "Jazz im Park – Ein Sommerabend mit Live-Musik und kulinarischen Highlights.\n\nEintritt: 15 € (ermäßigt 10 €), barrierefreier Zugang über Hauptportal, Anmeldung nicht erforderlich.\n\nGenießen Sie an einem lauen Sommerabend erstklassigen Jazz auf der Open-Air-Bühne. Neben dem Hauptact treten lokale Nachwuchsmusiker auf. Für das kulinarische Angebot sorgen Food-Trucks aus der Region.\n\nTickets: https://beispiel.de/tickets\nMehr Infos: https://beispiel.de"

    ## Validierung
    - Ende muss zeitlich nach Beginn liegen.
    - Ein Veranstaltungstag sollte nicht länger als 7 Tage dauern.
    - Der Titel sollte nicht leer sein und sinnvoll klingen.
    - Wenn ein Feld ungültig ist, erkläre freundlich warum und frage erneut.

    ## Zeitformat
    - Gib start_time und end_time im ISO-8601-Format mit UTC-Offset zurück.
    - Verwende immer die Zeitzone Europe/Berlin, z. B. "2025-07-12T18:00:00+02:00" (Sommerzeit) oder "2025-01-12T18:00:00+01:00" (Winterzeit).
    - Gib niemals ein Datum ohne Offset zurück.

    ## Kategorieauswahl
    - Wähle die category_id ausschließlich aus der Liste der verfügbaren Kategorien.
    - Passt eine Kategorie eindeutig (Titel + Beschreibung), wähle sie direkt.
    - Passen mehrere gleich gut oder bist du unsicher: Nenne im "reply" die 2 besten Vorschläge mit Name und ID und frage nach der Bestätigung. Lasse category_id dann weg.
    - Passt keine Kategorie: Frage nach einer präziseren Beschreibung, anstatt zu raten.

    ## Status
    - "incomplete": Mindestens ein Pflichtfeld fehlt oder ist ungültig.
    - "ready": Alle sechs Felder sind vollständig und validiert. Informiere den Nutzer dann freundlich, dass die Veranstaltung veröffentlicht werden kann.

    ## JSON-Regeln
    - Antworte in JEDEM Schritt mit einem gültigen JSON-Objekt.
    - Gib ALLE Felder aus dem Schema zurück.
    - Für Felder, die sich nicht geändert haben oder noch leer/unbekannt sind, verwende den Wert null.
    - In der Description darfst du Zeilenumbrüche als \n einfügen.
    - Keine zusätzlichen Properties außer denen im Schema.
    - Deine Antwort MUSS ausschließlich das JSON-Objekt enthalten, keinen einleitenden oder abschließenden Text.

    ## Beispiel für eine gültige Antwort
    Wenn der Nutzer nur den Titel ändert und die anderen Felder bereits bekannt sind:
    {
      "title": "Neuer Jazz im Park",
      "address": null,
      "description": null,
      "start_time": null,
      "end_time": null,
      "category_id": null,
      "status": "incomplete",
      "reply": "Titel angepasst! Bitte ergänze noch die Beschreibung."
    }
    """;

private const string JsonSchema = """
    {
        "type": "object",
        "properties": {
          "title":        { "type": ["string", "null"] },
          "address":      { "type": ["string", "null"] },
          "description":  { "type": ["string", "null"] },
          "start_time":   { "type": ["string", "null"] },
          "end_time":     { "type": ["string", "null"] },
          "category_id":  { "type": ["string", "null"] },
          "status":       { "type": "string", "enum": ["incomplete", "ready"] },
          "reply":        { "type": "string" }
        },
        "required": ["title", "address", "description", "start_time", "end_time", "category_id", "status", "reply"],
        "additionalProperties": false
    }
    """;

private string BuildSystemPrompt(Event @event, IEnumerable<EventCategory> categories)
{
    var sb = new System.Text.StringBuilder();
    
    sb.AppendLine(BaseSystemPrompt);
    sb.AppendLine();
    sb.AppendLine("--- AKTUELLER STAND DER VERANSTALTUNG ---");
    sb.AppendLine($"title: {(!string.IsNullOrWhiteSpace(@event.Title) ? @event.Title : "[FEHLT]")}");
    sb.AppendLine($"address: {(!string.IsNullOrWhiteSpace(@event.Address) ? @event.Address : "[FEHLT]")}");
    
    // Wichtig: Description so ausgeben, dass das LLM ihre Struktur sieht
    if (!string.IsNullOrWhiteSpace(@event.Description))
    {
        sb.AppendLine("description:");
        // Sicherstellen, dass \n im String zu echten Zeilenumbrüchen im Prompt werden
        sb.AppendLine(@event.Description.Replace("\\n", "\n"));
    }
    else
    {
        sb.AppendLine("description: [FEHLT]");
    }

    sb.AppendLine($"start_time: {(@event.StartTime.HasValue ? ToBerlinIsoString(@event.StartTime.Value) : "[FEHLT]")}");
    sb.AppendLine($"end_time: {(@event.EndTime.HasValue ? ToBerlinIsoString(@event.EndTime.Value) : "[FEHLT]")}");
    sb.AppendLine($"category_id: {(@event.EventCategoryId.HasValue ? @event.EventCategoryId.Value.ToString() : "[FEHLT]")} {(GetCategoryName(@event.EventCategoryId, categories))}");
    sb.AppendLine();
    sb.AppendLine("--- VERFÜGBARE KATEGORIEN (ID | Name) ---");
    foreach (var category in categories)
    {
        sb.AppendLine($"{category.Id} | {category.Name}");
    }
    sb.AppendLine();
    sb.AppendLine("--- AUFGABE ---");
    sb.AppendLine("Werte die Nutzernachricht aus. Aktualisiere bekannte Felder. Validiere. Frage nur nach fehlenden oder ungültigen Daten.");
    sb.AppendLine("Wenn der Nutzer neue Details nennt (Preise, Links etc.), integriere sie in die Description an den vorgesehenen Block.");
    
    return sb.ToString();
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
    
    private static string GetCategoryName(int? categoryId, IEnumerable<EventCategory> categories)
    {
        if (!categoryId.HasValue) return string.Empty;
        var cat = categories.FirstOrDefault(c => c.Id == categoryId.Value); // Achtung: Typ-Anpassung nötig, wenn Guid
        return cat != null ? $"({cat.Name})" : "(UNBEKANNTE KATEGORIE)";
    }
}
