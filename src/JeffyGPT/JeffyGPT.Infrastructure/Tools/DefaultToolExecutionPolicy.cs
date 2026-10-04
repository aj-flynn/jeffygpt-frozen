using JeffyGPT.Application.Chat;
using JeffyGPT.Application.Chat.Tools;

namespace JeffyGPT.Infrastructure.Tools;

public class DefaultToolExecutionPolicy : IToolExecutionPolicy
{
    public ToolExecutionPolicyResult Evaluate(IToolHandler handler)
    {
        var security = handler.Security;

        if (security.AccessKind == ToolAccessKind.Mutating)
        {
            return ToolExecutionPolicyResult.Denied("mutating tools are not permitted");
        }

        if (security.RequiresConfirmation)
        {
            return ToolExecutionPolicyResult.Denied("tool requires user confirmation");
        }

        return ToolExecutionPolicyResult.Allowed();
    }
}
