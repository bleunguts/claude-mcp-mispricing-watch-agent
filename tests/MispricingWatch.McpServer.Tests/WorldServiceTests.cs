using MispricingWatch.CurveLab;

namespace MispricingWatch.McpServer.Tests;

public class WorldServiceTests
{
    [TestCase("StalePrice", FaultType.StalePrice)]
    [TestCase("staleprice", FaultType.StalePrice)]
    [TestCase("SettingChange", FaultType.SettingChange)]
    [TestCase(null, FaultType.SettingChange)]
    [TestCase("nonsense", FaultType.SettingChange)]
    public void ParseFaultType_Value_ReturnsExpected(string? value, FaultType expected) =>
        Assert.That(WorldService.ParseFaultType(value), Is.EqualTo(expected));
}
