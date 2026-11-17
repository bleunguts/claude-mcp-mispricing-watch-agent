namespace MispricingWatch.Agent.Tests;

public class InvestigationGuardrailsTests
{
    private const string InsideTolerance =
        """{"available":true,"toleranceBps":1.0,"currentSpreadBps":-0.175,"score":-0.768,"ticksOutside":0}""";
    private const string OutsideTolerance =
        """{"available":true,"toleranceBps":1.0,"currentSpreadBps":6.2,"score":28.0,"ticksOutside":120}""";

    private static ToolExecution Ok(string name, string result = "{}") => new(name, new(), result, false);

    private static List<ToolExecution> AllSteps(string stats) =>
        [Ok("get_history"), Ok("get_benchmark"), Ok("compute_spread_stats", stats)];

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
}
