namespace JeffyGPT.Application.Chat.Tools;

public sealed record ToolExecutionPolicyResult(bool IsAllowed, string? ErrorMessage = null)
{
    public static ToolExecutionPolicyResult Allowed() => new(true);

    public static ToolExecutionPolicyResult Denied(string errorMessage) => new(false, errorMessage);
}
