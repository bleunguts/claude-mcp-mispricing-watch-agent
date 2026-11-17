namespace MispricingWatch.Agent.Tests;

public class InvestigationGuardrailsTests
{
    private const string InsideTolerance =
        """{"available":true,"toleranceBps":1.0,"currentSpreadBps":-0.175,"score":-0.768,"ticksOutside":0}""";
    private const string OutsideTolerance =
        """{"available":true,"toleranceBps":1.0,"currentSpreadBps":6.2,"score":28.0,"ticksOutside":120}""";

    private const string Stale =
        """{"tenor":"30Y","unchangedTicks":200,"hasBenchmark":true,"benchmarkMoveBps":12.0,"stale":true,"error":null}""";
    private const string NotStale =
        """{"tenor":"10Y","unchangedTicks":0,"hasBenchmark":true,"benchmarkMoveBps":0.0,"stale":false,"error":null}""";

    private static ToolExecution Ok(string name, string result = "{}") => new(name, new(), result, false);

    private static List<ToolExecution> AllSteps(string stats, string stale = NotStale) =>
        [Ok("get_history"), Ok("get_benchmark"), Ok("compute_spread_stats", stats), Ok("check_stale", stale)];

    [Test]
    public void Problem_BenchmarkNeverCalled_NamesTheMissingStep()
    {
        var problem = InvestigationGuardrails.Problem(
            [Ok("get_history"), Ok("compute_spread_stats", InsideTolerance)], "Verdict: OK");

        Assert.That(problem, Is.EqualTo("Call get_benchmark before you answer."));
    }

    [Test]
    public void Problem_FailedToolDoesNotCountAsDone()
    {
        var steps = AllSteps(InsideTolerance);
        steps[1] = new ToolExecution("get_benchmark", new(), "boom", true);

        Assert.That(InvestigationGuardrails.Problem(steps, "Verdict: OK"), Does.Contain("get_benchmark"));
    }

    [Test]
    public void Problem_NoVerdictLine_AsksForOne()
    {
        Assert.That(InvestigationGuardrails.Problem(AllSteps(InsideTolerance), "Looks fine."), Does.Contain("Verdict:"));
    }

    [Test]
    public void Problem_AlarmInsideTolerance_FlagsDisagreement()
    {
        // The live Qwen 3b mistake: ALARM on a gap of -0.175 bps against a tolerance of 1 bp.
        var problem = InvestigationGuardrails.Problem(AllSteps(InsideTolerance), "Verdict: ALARM\nRationale: x");

        Assert.That(problem, Does.Contain("must be OK"));
    }

    [Test]
    public void Problem_OkOutsideTolerance_FlagsDisagreement()
    {
        var problem = InvestigationGuardrails.Problem(AllSteps(OutsideTolerance), "Verdict: OK\nRationale: x");

        Assert.That(problem, Does.Contain("must be ALARM"));
    }

    [Test]
    public void Problem_VerdictAgreesWithStats_ReturnsNull()
    {
        Assert.That(InvestigationGuardrails.Problem(AllSteps(InsideTolerance), "Verdict: OK\nRationale: x"), Is.Null);
        Assert.That(InvestigationGuardrails.Problem(AllSteps(OutsideTolerance), "Verdict: ALARM\nRationale: x"), Is.Null);
    }

    [Test]
    public void Problem_NoBenchmarkAvailable_SkipsTheAgreementCheck()
    {
        var problem = InvestigationGuardrails.Problem(AllSteps("""{"available":false}"""), "Verdict: OK\nRationale: x");

        Assert.That(problem, Is.Null);
    }

    [Test]
    public void Problem_CheckStaleNeverCalled_NamesTheMissingStep()
    {
        var steps = new List<ToolExecution>
            { Ok("get_history"), Ok("get_benchmark"), Ok("compute_spread_stats", InsideTolerance) };

        Assert.That(InvestigationGuardrails.Problem(steps, "Verdict: OK\nRationale: x"), Is.EqualTo("Call check_stale before you answer."));
    }

    [Test]
    public void Problem_StaleButInsideTolerance_OkVerdict_FlagsDisagreement()
    {
        // The Claude Desktop miss on prompt v1: the price was frozen, the gap was still inside tolerance.
        var problem = InvestigationGuardrails.Problem(AllSteps(InsideTolerance, Stale), "Verdict: OK\nRationale: x");

        Assert.That(problem, Does.Contain("check_stale"));
        Assert.That(problem, Does.Contain("must be ALARM"));
    }

    [Test]
    public void Problem_StaleButInsideTolerance_AlarmVerdict_ReturnsNull()
    {
        Assert.That(InvestigationGuardrails.Problem(AllSteps(InsideTolerance, Stale), "Verdict: ALARM\nRationale: x"), Is.Null);
    }

    [Test]
    public void Problem_NoBenchmarkButStale_OkVerdict_FlagsDisagreement()
    {
        var problem = InvestigationGuardrails.Problem(AllSteps("""{"available":false}""", Stale), "Verdict: OK\nRationale: x");

        Assert.That(problem, Does.Contain("check_stale"));
    }

    [TestCase(InsideTolerance, NotStale, false)]
    [TestCase(OutsideTolerance, NotStale, true)]
    [TestCase(InsideTolerance, Stale, true)]
    [TestCase("""{"available":false}""", Stale, true)]
    public void ExpectedAlarm_SpreadOrStale_AlarmWhenEitherFires(string stats, string stale, bool expected)
    {
        Assert.That(InvestigationGuardrails.ExpectedAlarm(stats, stale), Is.EqualTo(expected));
    }

    [Test]
    public void ExpectedAlarm_NoBenchmarkAndNotStale_ReturnsNull()
    {
        Assert.That(InvestigationGuardrails.ExpectedAlarm("""{"available":false}""", NotStale), Is.Null);
    }

    [Test]
    public void Problem_OutsideToleranceButNotStale_AlarmVerdict_ReturnsNull()
    {
        Assert.That(InvestigationGuardrails.Problem(AllSteps(OutsideTolerance, NotStale), "Verdict: ALARM\nRationale: x"), Is.Null);
    }
}
