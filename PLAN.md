Lass uns jetzt den Ai Bot planen. Wir haben bereits die Infrastruktur geschaffen. 
Der Bot soll bei der Erstellung von events helfen. 
Über natürliche Sprache soll der Nutzer die Informatonen zu seiner Veranstaltung eingeben.
Der Bot extrahiert die Informationen in ein maschinell auslesbares Format.
In einem Prototypen habe ich das so gemacht:
´
using System.Text.Json;
using EventChatBot.Api.Models;
using EventChatBot.Api.Repositories;
using OpenAI;
using OpenAI.Chat;

namespace EventChatBot.Api.Handlers;

public class ConversationsHandler(
ConversationsRepository conversationsRepository,
MessagesRepository messagesRepository,
EventDraftsRepository eventDraftsRepository,
OpenAIClient openAiClient)
{
private const string FirstMessage = "Hallo! Erzähl mir von deiner geplanten Veranstaltung.";

    private const string SystemPrompt = """
        Du bist ein freundlicher Assistent, der Vereinsvertretern hilft, ihre Veranstaltung für den Kulturkalender zu erfassen.
        Frage nach diesen sechs Feldern: Name der Veranstaltung, Ort, Beschreibung, Kategorie, Beginn (Datum + Uhrzeit) und Ende (Datum + Uhrzeit).
        Frage immer nur nach einer fehlenden Information auf einmal. Antworte ausschließlich auf Deutsch.
        Setze "status" auf "ready", wenn alle sechs Felder vollständig und plausibel sind (Ende muss nach Beginn liegen).
        Gib Beginn und Ende im ISO-8601-Format zurück, z.B. "2025-07-12T18:00:00".
        Setze Felder in "extracted_data" auf null, wenn die Information noch nicht bekannt ist.
        """;

    private static readonly BinaryData JsonSchema = BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "message":        { "type": "string" },
                "status":         { "type": "string", "enum": ["incomplete", "ready"] },
                "extracted_data": {
                    "type": "object",
                    "properties": {
                        "name":         { "type": ["string", "null"] },
                        "ort":          { "type": ["string", "null"] },
                        "beschreibung": { "type": ["string", "null"] },
                        "kategorie":    { "type": ["string", "null"] },
                        "beginn":       { "type": ["string", "null"] },
                        "ende":         { "type": ["string", "null"] }
                    },
                    "required": ["name", "ort", "beschreibung", "kategorie", "beginn", "ende"],
                    "additionalProperties": false
                }
            },
            "required": ["message", "status", "extracted_data"],
            "additionalProperties": false
        }
        """);

    public async Task<SendMessageResponse?> SendMessageAsync(Guid conversationId, Guid userId, string content)
    {
        var conversation = await conversationsRepository.GetByIdAsync(conversationId, userId);
        if (conversation is null) return null;

        await messagesRepository.CreateAsync(conversationId, "user", content);

        var history = await messagesRepository.GetByConversationIdAsync(conversationId);

        var chatMessages = new List<ChatMessage>
        {
            ChatMessage.CreateSystemMessage(SystemPrompt)
        };
        chatMessages.AddRange(history.Select(m => m.Role == "user"
            ? ChatMessage.CreateUserMessage(m.Content)
            : (ChatMessage)ChatMessage.CreateAssistantMessage(m.Content)));

        var response = await openAiClient.GetChatClient("gpt-4o-mini").CompleteChatAsync(
            chatMessages,
            new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    "event_extraction",
                    jsonSchema: JsonSchema,
                    jsonSchemaIsStrict: true)
            });

        var llmJson = response.Value.Content[0].Text;
        var llmResponse = JsonSerializer.Deserialize<LlmResponse>(llmJson)!;

        await eventDraftsRepository.UpdateAsync(conversationId, llmResponse.ExtractedData);

        await messagesRepository.CreateAsync(conversationId, "assistant", llmResponse.Message);

        return new SendMessageResponse(llmResponse.Message);
    }
}

´

Beachte, dass dieser Bot später auch von anderen Features (z.B. Berichte, den Vereinssteckbrief) genutzt werden.
Daher sollten wir die Struktur und die notwendigen Dateien dafür vorbereitend planen.
Denn jedes Feature wird seinen eigenen Prepromt und sein eigenes json Format bekommen.

Für die Anfang starten wir mit den events. 
Schau dir das Model in der für die Event Datenbank an. 
Diese Felder sollen enstprechend vom ausgelesen und gespeichert werden.

Die Antwort soll im Frontend an die Liste aus dem getConversation mit den Messages angehangen werden.

Installiere den openAi Client. So kannst du ihn konfigurieren:
´
var apiKey = configuration["OpenAI:ApiKey"]
?? throw new InvalidOperationException("OpenAI:ApiKey is not configured.");
services.AddSingleton(new OpenAIClient(apiKey));
´
Passe die appsettings.json an.