using ModelContextProtocol.Client;
using OpenTelemetry;
using OpenTelemetry.Trace;
using System.Diagnostics;

namespace MispricingWatch.Agent.Tests;

// These tests launch the real MCP server (built dll, stdio) and drive the loop with a scripted model.
public class AgentLoopTests
{
    private McpClient _client = null!;
    private List<ToolDefinition> _tools = null!;

    [OneTimeSetUp]
    public async Task StartServer()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "MispricingWatch.slnx")))
        {
            root = Path.GetDirectoryName(root) ?? throw new InvalidOperationException("Solution folder not found.");
        }
        var serverDll = Path.Combine(root, "src", "MispricingWatch.McpServer", "bin", "Debug", "net10.0", "MispricingWatch.McpServer.dll");

        _client = await McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "MispricingWatch.McpServer",
            Command = "dotnet",
            Arguments = [serverDll],
        }));
        _tools = (await _client.ListToolsAsync())
            .Select(t => new ToolDefinition(t.Name, t.Description, t.JsonSchema))
            .ToList();
    }

    [OneTimeTearDown]
    public async Task StopServer() => await _client.DisposeAsync();

    private static Func<List<Message>, ModelResponse> Step(ModelResponse r) => _ => r;

    private static Dictionary<string, object?> NoArgs() => new();

    [Test]
    public async Task RunAsync_ToolThenAnswer_CompletesAndFeedsResultBack()
    {
        var model = new ScriptedModel([
            Step(Script.CallTool("t1", "ping", NoArgs())),
            Step(Script.Finish("done"))]);

        var result = await AgentLoop.RunAsync(model, _client, _tools, "go", 5);

        Assert.That(result.StopReason, Is.EqualTo(LoopStopReason.Completed));
        Assert.That(result.FinalText, Is.EqualTo("done"));
        Assert.That(result.Iterations, Is.EqualTo(2));
        // user, assistant(tool_use), user(tool_result), assistant(final)
        Assert.That(result.Messages, Has.Count.EqualTo(4));
        var toolResult = (ToolResultBlock)result.Messages[2].Content.Single();
        Assert.That(toolResult.ToolUseId, Is.EqualTo("t1"));
        Assert.That(toolResult.IsError, Is.False);
        Assert.That(toolResult.Content, Is.Not.Empty);
    }

    [Test]
    public async Task RunAsync_ModelNeverStops_StopsAtIterationCap()
    {
        var runaway = new ScriptedModel(Enumerable.Range(0, 50)
            .Select(i => Step(Script.CallTool($"t{i}", "ping", NoArgs()))));

        var result = await AgentLoop.RunAsync(runaway, _client, _tools, "go", 3);

        Assert.That(result.StopReason, Is.EqualTo(LoopStopReason.IterationCap));
        Assert.That(result.Iterations, Is.EqualTo(3));
    }

    [Test]
    public async Task RunAsync_TwoToolCallsInOneTurn_RunsFirstAndErrorsTheRest()
    {
        var two = new ModelResponse(
            [new ToolUseBlock("a", "ping", NoArgs()), new ToolUseBlock("b", "ping", NoArgs())],
            StopReason.ToolUse);
        var model = new ScriptedModel([Step(two), Step(Script.Finish("ok"))]);

        var result = await AgentLoop.RunAsync(model, _client, _tools, "go", 5);

        var results = result.Messages[2].Content.Cast<ToolResultBlock>().ToList();
        Assert.That(results.Select(r => r.ToolUseId), Is.EqualTo(new[] { "a", "b" }));
        Assert.That(results.Select(r => r.IsError), Is.EqualTo(new[] { false, true }));
    }

    [Test]
    public async Task RunAsync_AnswerBeforeMethodDone_IsNudgedThenAccepted()
    {
        var model = new ScriptedModel([
            Step(Script.Finish("Verdict: OK\nRationale: guess")),
            Step(Script.CallTool("t1", "get_history", new() { ["tenor"] = "10Y", ["window"] = "1D" })),
            Step(Script.CallTool("t2", "get_benchmark", new() { ["tenor"] = "10Y", ["window"] = "1D" })),
            Step(Script.CallTool("t3", "compute_spread_stats", new() { ["tenor"] = "10Y" })),
            Step(Script.CallTool("t4", "check_stale", new() { ["tenor"] = "10Y" })),
            Step(Script.Finish("Verdict: OK\nRationale: inside tolerance"))]);

        var result = await AgentLoop.RunAsync(
            model, _client, _tools, "go", 10, answerProblem: InvestigationGuardrails.Problem);

        Assert.That(result.Incomplete, Is.False);
        Assert.That(result.Iterations, Is.EqualTo(6));
        var nudge = (TextBlock)result.Messages[2].Content.Single();
        Assert.That(nudge.Text, Does.StartWith("You have not finished. Call get_history"));
    }

    [Test]
    public async Task RunAsync_ModelIgnoresNudges_StopsIncompleteAfterNudgeLimit()
    {
        var model = new ScriptedModel(Enumerable.Range(0, 10)
            .Select(_ => Step(Script.Finish("Verdict: OK\nRationale: guess"))));

        var result = await AgentLoop.RunAsync(
            model, _client, _tools, "go", 10, answerProblem: InvestigationGuardrails.Problem, maxNudges: 2);

        Assert.That(result.StopReason, Is.EqualTo(LoopStopReason.Completed));
        Assert.That(result.Incomplete, Is.True);
        Assert.That(result.Iterations, Is.EqualTo(3)); // the first answer plus two nudged retries
        Assert.That(result.Problem, Does.Contain("get_history"));
    }

    [Test]
    public async Task RunAsync_UnknownTool_ReturnsErrorResultAndContinues()
    {
        var model = new ScriptedModel([
            Step(Script.CallTool("t1", "no_such_tool", NoArgs())),
            Step(Script.Finish("recovered"))]);

        var result = await AgentLoop.RunAsync(model, _client, _tools, "go", 5);

        Assert.That(result.StopReason, Is.EqualTo(LoopStopReason.Completed));
        var toolResult = (ToolResultBlock)result.Messages[2].Content.Single();
        Assert.That(toolResult.IsError, Is.True);
    }

    [Test]
    public async Task RunAsync_ToolThenAnswer_EmitsSpanPerModelCallAndToolCall()
    {
        var spans = new List<Activity>();
        using var tracer = Sdk.CreateTracerProviderBuilder()
            .AddSource(AgentTelemetry.SourceName)
            .AddInMemoryExporter(spans)
            .Build();
        var model = new ScriptedModel([
            Step(Script.CallTool("t1", "ping", NoArgs())),
        Step(Script.Finish("done"))]);

        await AgentLoop.RunAsync(model, _client, _tools, "go", 5);
        tracer.ForceFlush();

        Assert.That(spans.Count(s => s.DisplayName == "model.complete"), Is.EqualTo(2));
        Assert.That(spans.Count(s => s.DisplayName == "tool ping"), Is.EqualTo(1));
    }
}
