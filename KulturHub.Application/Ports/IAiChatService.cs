namespace KulturHub.Application.Ports;

public record AiMessage(string Role, string Content);

public interface IAiChatService
{
    Task<string> GetStructuredReplyAsync(
        string systemPrompt,
        string jsonSchema,
        IEnumerable<AiMessage> history,
        CancellationToken cancellationToken = default);
}
