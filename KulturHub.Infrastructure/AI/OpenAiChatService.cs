using KulturHub.Application.Ports;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Chat;

namespace KulturHub.Infrastructure.AI;

public class OpenAiChatService(OpenAIClient client, IConfiguration configuration) : IAiChatService
{
    private string Model => configuration["OpenAI:Model"] ?? "gpt-4o";

    public async Task<string> GetStructuredReplyAsync(
        string systemPrompt,
        string jsonSchema,
        IEnumerable<AiMessage> history,
        CancellationToken cancellationToken = default)
    {
        var messages = new List<ChatMessage> { new SystemChatMessage(systemPrompt) };

        foreach (var msg in history)
        {
            messages.Add(msg.Role == "user"
                ? new UserChatMessage(msg.Content)
                : (ChatMessage)new AssistantChatMessage(msg.Content));
        }

        var options = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "event_draft",
                jsonSchema: BinaryData.FromString(jsonSchema),
                jsonSchemaIsStrict: true)
        };

        var response = await client.GetChatClient(Model).CompleteChatAsync(messages, options, cancellationToken);
        return response.Value.Content[0].Text;
    }
}
