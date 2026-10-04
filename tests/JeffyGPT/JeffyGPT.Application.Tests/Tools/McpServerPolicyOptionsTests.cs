using JeffyGPT.Infrastructure.Configuration;

namespace JeffyGPT.Application.Tests.Tools;

public class McpServerPolicyOptionsTests
{
    [Fact]
    public void IsValid_AllowAllTools_IsValidWithoutApprovedList()
    {
        var policy = new McpServerPolicyOptions { AllowAllTools = true };

        var valid = policy.IsValid(out var error);

        Assert.True(valid);
        Assert.Equal("", error);
    }

    [Fact]
    public void IsValid_ApprovedList_IsValidWhenAllowAllIsFalse()
    {
        var policy = new McpServerPolicyOptions
        {
            AllowAllTools = false,
            AllowedTools = ["search"]
        };

        var valid = policy.IsValid(out var error);

        Assert.True(valid);
        Assert.Equal("", error);
    }

    [Fact]
    public void IsValid_NeitherAllowAllNorApprovedList_IsInvalid()
    {
        var policy = new McpServerPolicyOptions { AllowAllTools = false };

        var valid = policy.IsValid(out var error);

        Assert.False(valid);
        Assert.Contains("AllowedTools", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IsValid_WhitespaceOnlyApprovedList_IsInvalid()
    {
        var policy = new McpServerPolicyOptions
        {
            AllowAllTools = false,
            AllowedTools = ["  ", ""]
        };

        var valid = policy.IsValid(out var error);

        Assert.False(valid);
        Assert.Contains("AllowedTools", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AllowsTool_AllowAllTools_IgnoresApprovedList()
    {
        var policy = new McpServerPolicyOptions
        {
            AllowAllTools = true,
            AllowedTools = ["search"]
        };

        Assert.True(policy.AllowsTool("delete-everything"));
    }

    [Fact]
    public void AllowsTool_ApprovedList_AllowsListedAndDeniesOthers()
    {
        var policy = new McpServerPolicyOptions
        {
            AllowAllTools = false,
            AllowedTools = ["search"]
        };

        Assert.True(policy.AllowsTool("search"));
        Assert.False(policy.AllowsTool("read-file"));
    }
}
