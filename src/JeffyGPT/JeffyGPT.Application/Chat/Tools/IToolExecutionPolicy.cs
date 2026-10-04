namespace JeffyGPT.Application.Chat.Tools;

public interface IToolExecutionPolicy
{
    ToolExecutionPolicyResult Evaluate(IToolHandler handler);
}
