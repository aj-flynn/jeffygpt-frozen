namespace JeffyGPT.Application.Chat;

/// <summary>
/// Malformed ids and ids this process did not issue. The endpoint writes <see cref="Exception.Message"/> as 404.
/// </summary>
public sealed class ConversationNotFoundException : Exception
{
    public const string ErrorText = "Conversation not found.";

    public ConversationNotFoundException()
        : base(ErrorText)
    {
    }
}

/// <summary>
/// This conversation is already in a turn. The endpoint writes <see cref="Exception.Message"/> as 409.
/// </summary>
public sealed class ConversationBusyException : Exception
{
    public const string ErrorText = "A turn is already in progress.";

    public ConversationBusyException()
        : base(ErrorText)
    {
    }
}
