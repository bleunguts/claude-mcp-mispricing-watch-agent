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

    [TestCase("true", true)]
    [TestCase("TRUE", true)]
    [TestCase("1", true)]
    [TestCase("false", false)]
    [TestCase("0", false)]
    [TestCase(null, false)]
    [TestCase("nonsense", false)]
    public void ParseQuiet_Value_ReturnsExpected(string? value, bool expected) =>
        Assert.That(WorldService.ParseQuiet(value), Is.EqualTo(expected));
}
