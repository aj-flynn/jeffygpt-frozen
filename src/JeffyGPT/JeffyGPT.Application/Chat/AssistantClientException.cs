namespace JeffyGPT.Application.Chat;

public sealed class AssistantClientException : Exception
{
    public AssistantClientException(string message)
        : base(message)
    {
    }

    public AssistantClientException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
