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
    IOrganisationRepository organisationRepository,
    IEventRepository eventRepository,
    IMessageRepository messageRepository,
    IAiChatService aiChatService,
    IUnitOfWork unitOfWork) : ISendMessageService
{
    private const string SystemPrompt = """
        Du bist ein freundlicher Assistent, der Vereinsvertretern hilft, ihre Veranstaltung für den Kulturkalender zu erfassen.
        Frage nach diesen fünf Feldern: Titel der Veranstaltung, Adresse/Ort, Beschreibung, Beginn (Datum + Uhrzeit) und Ende (Datum + Uhrzeit).
        Frage immer nur nach einer fehlenden Information auf einmal. Antworte ausschließlich auf Deutsch.
        Setze "status" auf "ready", wenn alle fünf Felder vollständig und plausibel sind (Ende muss nach Beginn liegen).
        Gib Beginn und Ende im ISO-8601-Format zurück, z.B. "2025-07-12T18:00:00".
        Felder, die noch nicht bekannt sind, lasse im JSON weg.
        Wenn alle benötigten Felder mit Daten gefüllt sind, kannst du dem Nutzer Bescheid geben, dass er die Veranstaltung veröffentlichen kann.
        """;

    private const string JsonSchema = """
        {
          "type": "object",
          "properties": {
            "title":       { "type": "string" },
            "address":     { "type": "string" },
            "description": { "type": "string" },
            "start_time":  { "type": "string" },
            "end_time":    { "type": "string" },
            "status":      { "type": "string", "enum": ["incomplete", "ready"] },
            "reply":       { "type": "string" }
          },
          "required": ["status", "reply"],
          "additionalProperties": false
        }
        """;

    public async Task<ErrorOr<SendMessageResponse>> SendMessageAsync(
        SendMessageInput input, CancellationToken cancellationToken = default)
    {
        var isMember = await organisationRepository.IsMemberAsync(input.OrganisationId, input.UserId);
        if (!isMember) return OrganisationErrors.Forbidden();

        var @event = await eventRepository.GetByIdAsync(input.EventId, input.OrganisationId);
        if (@event is null) return EventErrors.NotFound(input.EventId);
        if (@event.ConversationId is null) return EventErrors.NoConversation(input.EventId);

        var allMessages = await messageRepository.GetByConversationIdAsync(@event.ConversationId.Value);
        var history = allMessages
            .Select(m => new AiMessage(
                Role: m.Role == MessageRole.User ? "user" : "assistant",
                Content: m.Content))
            .Append(new AiMessage("user", input.Content))
            .ToList();

        var jsonReply = await aiChatService.GetStructuredReplyAsync(
            SystemPrompt, JsonSchema, history, cancellationToken);

        var aiResponse = JsonSerializer.Deserialize<EventAiResponse>(jsonReply)
            ?? throw new InvalidOperationException("AI returned unparseable JSON.");

        DateTime? parsedStart = aiResponse.StartTime is not null
            ? DateTime.Parse(aiResponse.StartTime, null, DateTimeStyles.RoundtripKind).ToUniversalTime()
            : null;
        DateTime? parsedEnd = aiResponse.EndTime is not null
            ? DateTime.Parse(aiResponse.EndTime, null, DateTimeStyles.RoundtripKind).ToUniversalTime()
            : null;

        var newStatus = aiResponse.Status == "ready" ? EventStatus.ReadyToPublish : EventStatus.Draft;
        @event.UpdateDraft(aiResponse.Title, aiResponse.Address, aiResponse.Description,
                           parsedStart, parsedEnd, newStatus);

        var userMessage = Message.Create(@event.ConversationId.Value, MessageRole.User, input.Content);
        var botMessage = Message.Create(@event.ConversationId.Value, MessageRole.System, aiResponse.Reply);

        await unitOfWork.BeginAsync();
        await messageRepository.CreateAsync(userMessage);
        await eventRepository.UpdateDraftAsync(@event);
        await messageRepository.CreateAsync(botMessage);
        await unitOfWork.CommitAsync();

        return new SendMessageResponse(
            new MessageResponse(userMessage.Id, userMessage.Role, userMessage.Content, userMessage.CreatedAt),
            new MessageResponse(botMessage.Id, botMessage.Role, botMessage.Content, botMessage.CreatedAt));
    }
}
