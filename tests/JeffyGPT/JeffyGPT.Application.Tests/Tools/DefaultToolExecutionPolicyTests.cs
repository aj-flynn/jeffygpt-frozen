using JeffyGPT.Application.Chat;
using JeffyGPT.Application.Chat.Tools;
using JeffyGPT.Application.Tests.TestDoubles;
using JeffyGPT.Infrastructure.Tools;

namespace JeffyGPT.Application.Tests.Tools;

public class DefaultToolExecutionPolicyTests
{
    private static DefaultToolExecutionPolicy CreatePolicy() => new();

    private static IToolHandler CreateHandler(ToolSecurityDescriptor security) =>
        new ConfigurableSecurityToolHandler(security);

    [Fact]
    public void Evaluate_ReadOnlyNonFilesystemWithoutConfirmation_Allows()
    {
        var policy = CreatePolicy();
        var handler = CreateHandler(new ToolSecurityDescriptor(
            ToolAccessKind.ReadOnly,
            ToolDataExposure.RuntimeMetadata,
            RequiresConfirmation: false));

        var result = policy.Evaluate(handler);

        Assert.True(result.IsAllowed);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Evaluate_MutatingTool_Denies()
    {
        var policy = CreatePolicy();
        var handler = CreateHandler(new ToolSecurityDescriptor(
            ToolAccessKind.Mutating,
            ToolDataExposure.None,
            RequiresConfirmation: false));

        var result = policy.Evaluate(handler);

        Assert.False(result.IsAllowed);
        Assert.Equal("mutating tools are not permitted", result.ErrorMessage);
    }

    [Fact]
    public void Evaluate_RequiresConfirmation_Denies()
    {
        var policy = CreatePolicy();
        var handler = CreateHandler(new ToolSecurityDescriptor(
            ToolAccessKind.ReadOnly,
            ToolDataExposure.None,
            RequiresConfirmation: true));

        var result = policy.Evaluate(handler);

        Assert.False(result.IsAllowed);
        Assert.Equal("tool requires user confirmation", result.ErrorMessage);
    }
}
