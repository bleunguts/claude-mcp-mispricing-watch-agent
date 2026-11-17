namespace MispricingWatch.Agent.Tests;

public class AlarmRecordTests
{
    [Test]
    public void Parse_AlarmVerdict_ReturnsAlarmWithRationale()
    {
        var record = AlarmRecord.Parse("Checked.\nVerdict: ALARM\nRationale: score 6.2, ticksOutside 120.", "10Y");

        Assert.That(record, Is.Not.Null);
        Assert.That(record!.Alarm, Is.True);
        Assert.That(record.Node, Is.EqualTo("10Y"));
        Assert.That(record.Detector, Is.EqualTo("spread-to-benchmark"));
        Assert.That(record.Rationale, Is.EqualTo("score 6.2, ticksOutside 120."));
    }

    [Test]
    public void Parse_OkVerdictLowerCase_ReturnsNoAlarm()
    {
        var record = AlarmRecord.Parse("verdict: ok\nrationale: small score.", "5Y");

        Assert.That(record!.Alarm, Is.False);
        Assert.That(record.Detector, Is.EqualTo("none"));
    }

    [Test]
    public void Parse_NoVerdictLine_ReturnsNull()
    {
        Assert.That(AlarmRecord.Parse("It looks fine to me.", "10Y"), Is.Null);
    }
}
