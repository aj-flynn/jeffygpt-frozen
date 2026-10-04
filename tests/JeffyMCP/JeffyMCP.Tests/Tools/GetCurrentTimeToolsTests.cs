using System.Text.Json;
using JeffyMCP.Tools;

namespace JeffyMCP.Tests.Tools;

public class GetCurrentTimeToolsTests
{
    [Fact]
    public void GetCurrentTime_ReturnsLocalJson()
    {
        var before = DateTimeOffset.Now;

        var result = GetCurrentTimeTools.GetCurrentTime();

        var after = DateTimeOffset.Now;
        using var document = JsonDocument.Parse(result);
        Assert.True(document.RootElement.TryGetProperty("local", out var localProperty));

        var localValue = localProperty.GetString();
        Assert.NotNull(localValue);
        Assert.True(DateTimeOffset.TryParse(localValue, out var parsedLocal));
        Assert.Equal(TimeZoneInfo.Local.GetUtcOffset(parsedLocal), parsedLocal.Offset);
        Assert.InRange(parsedLocal, before, after);
    }
}
