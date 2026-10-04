using JeffyGPT.Contracts.Chat;

namespace JeffyGPT.Application.Chat;

public static class ChatRequestValidator
{
    public const int MaxMessageCount = 50;
    public const int MaxContentLengthPerMessage = 32_000;
    public const int MaxTotalContentLength = 128_000;

    public static void Validate(ChatRequest request)
    {
        if (request.Messages is null || request.Messages.Count == 0)
        {
            throw new ArgumentException("Messages cannot be null or empty.");
        }

        if (request.Messages.Count > MaxMessageCount)
        {
            throw new ArgumentException($"At most {MaxMessageCount} messages are allowed.");
        }

        var totalContentLength = 0;

        foreach (var message in request.Messages)
        {
            if (message is null)
            {
                throw new ArgumentException("Messages cannot contain null entries.");
            }

            if (!IsAllowedClientRole(message.Role))
            {
                throw new ArgumentException(
                    "Only 'user' and 'assistant' roles are allowed in client requests.");
            }

            if (!string.IsNullOrEmpty(message.ToolCallId))
            {
                throw new ArgumentException("Client messages cannot include toolCallId.");
            }

            if (message.ToolCalls is { Count: > 0 })
            {
                throw new ArgumentException("Client messages cannot include toolCalls.");
            }

            var contentLength = message.Content?.Length ?? 0;
            if (contentLength > MaxContentLengthPerMessage)
            {
                throw new ArgumentException(
                    $"Each message content must be at most {MaxContentLengthPerMessage} characters.");
            }

            totalContentLength += contentLength;
            if (totalContentLength > MaxTotalContentLength)
            {
                throw new ArgumentException(
                    $"Total message content must be at most {MaxTotalContentLength} characters.");
            }
        }

        var hasUserMessage = request.Messages.Any(m =>
            string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase));

        if (!hasUserMessage)
        {
            throw new ArgumentException("A user message is required.");
        }
    }

    internal static bool IsAllowedClientRole(string? role) =>
        string.Equals(role, "user", StringComparison.OrdinalIgnoreCase)
        || string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase);
}
