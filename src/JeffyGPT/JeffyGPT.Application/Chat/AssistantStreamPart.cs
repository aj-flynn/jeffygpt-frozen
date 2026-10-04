using JeffyGPT.Contracts.Chat;

namespace JeffyGPT.Application.Chat;

public abstract record AssistantStreamPart;

public sealed record AssistantContentDelta(string Content) : AssistantStreamPart;

public sealed record AssistantCompletionFinished(AssistantCompletion Completion) : AssistantStreamPart;
