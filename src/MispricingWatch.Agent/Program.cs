// Agent entry point: the tool ORCHESTRATOR. Starts the built MCP server over stdio, lists its tools, then runs
// the hand-written loop (AgentLoop) on a scripted model. No LLM is involved yet (live Qwen run is #56).
using MispricingWatch.Agent;
using ModelContextProtocol.Client;

var root = AppContext.BaseDirectory;
while (!File.Exists(Path.Combine(root, "MispricingWatch.slnx")))
{
    root = Path.GetDirectoryName(root) ?? throw new InvalidOperationException("Solution folder not found.");
}
var serverDll = Path.Combine(root, "src", "MispricingWatch.McpServer", "bin", "Debug", "net10.0", "MispricingWatch.McpServer.dll");
Console.WriteLine($"Starting MCP server: {serverDll} (exists: {File.Exists(serverDll)})");

var transport = new StdioClientTransport(new StdioClientTransportOptions
{
    Name = "MispricingWatch.McpServer",
    Command = "dotnet",
    Arguments = [serverDll],
});
await using var client = await McpClient.CreateAsync(transport);

var tools = (await client.ListToolsAsync())
    .Select(t => new ToolDefinition(t.Name, t.Description, t.JsonSchema))
    .ToList();
Console.WriteLine($"Tools: {string.Join(", ", tools.Select(t => t.Name))}");

// Live runs on local Ollama:
//   dotnet run --project src/MispricingWatch.Agent -- live  [model] [tenor]      one run, full transcript
//   dotnet run --project src/MispricingWatch.Agent -- batch [model] [tenor] [n]  n runs, one summary row each
if (args.Length > 0 && (args[0] == "live" || args[0] == "batch"))
{
    var modelName = args.Length > 1 ? args[1] : "qwen2.5:3b";
    var tenor = args.Length > 2 ? args[2] : "10Y";
    var runs = args[0] == "batch" && args.Length > 3 ? int.Parse(args[3]) : 1;
    using var http = new HttpClient { BaseAddress = new Uri("http://localhost:11434"), Timeout = TimeSpan.FromMinutes(10) };

    // What the detector itself says about this node, read the same way the agent can. Used to mark answers right or wrong.
    var statsReply = await client.CallToolAsync("compute_spread_stats", new Dictionary<string, object?> { ["tenor"] = tenor });
    var expected = InvestigationGuardrails.ExpectedAlarm(
        string.Join("", statsReply.Content.OfType<ModelContextProtocol.Protocol.TextContentBlock>().Select(c => c.Text)));
    var expectedText = expected is { } e ? (e.Alarm ? "ALARM" : "OK") : "n/a (no benchmark)";
    Console.WriteLine($"\n=== {modelName} on {tenor}, prompt {InvestigationPrompt.Version}, {runs} run(s); spread stats say: {expectedText} ===");

    var rows = new List<string>();
    for (var run = 1; run <= runs; run++)
    {
        Console.WriteLine($"\n--- run {run} ---");
        var clock = System.Diagnostics.Stopwatch.StartNew();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10)); // a stalled model cannot hang the run
        try
        {
            var live = await AgentLoop.RunAsync(
                new OllamaModel(http, modelName), client, tools, InvestigationPrompt.Question(tenor), maxIterations: 10,
                InvestigationPrompt.System, InvestigationGuardrails.Problem, cancellationToken: timeout.Token);
            Report(live);
            Console.WriteLine(AlarmRecord.Parse(live.FinalText, tenor)?.ToJson() ?? "No usable Verdict line.");
            if (live.Incomplete)
            {
                Console.WriteLine($"INCOMPLETE: {live.Problem}");
            }
            rows.Add(SummaryRow(run, live, expected, clock.Elapsed));
        }
        catch (Exception ex) when (ex is OperationCanceledException or InvalidOperationException)
        {
            Console.WriteLine($"RUN FAILED: {ex.Message}");
            rows.Add($"{run,3} {clock.Elapsed.TotalSeconds,5:F0}s  FAILED ({ex.GetType().Name})");
        }
    }

    Console.WriteLine($"\n=== Summary: {modelName} on {tenor}, expected {expectedText} ===");
    Console.WriteLine("run  time  iters nudges first  final  right? incomplete");
    rows.ForEach(Console.WriteLine);
    return;
}

// Run 1: the scripted investigation. The model "decides" to call three tools, then answers.
Console.WriteLine("\n=== Scripted run: history, benchmark, spread stats, answer ===");
var script = new ScriptedModel([
    _ => Script.CallTool("t1", "get_history", new() { ["tenor"] = "10Y", ["window"] = "1D" }),
    _ => Script.CallTool("t2", "get_benchmark", new() { ["tenor"] = "10Y", ["window"] = "1D" }),
    _ => Script.CallTool("t3", "compute_spread_stats", new() { ["tenor"] = "10Y" }),
    _ => Script.Finish("Scripted answer: I looked at history, benchmark and spread stats for 10Y."),
]);
Report(await AgentLoop.RunAsync(script, client, tools, "Is the 10Y price a real market price?", maxIterations: 10));

// Run 2: a model that never stops asking for tools. The loop's cap must end it.
Console.WriteLine("\n=== Runaway run: the cap must stop it ===");
var runaway = new ScriptedModel(Enumerable.Range(0, 50)
    .Select<int, Func<List<Message>, ModelResponse>>(i => _ => Script.CallTool($"r{i}", "ping", new())));
Report(await AgentLoop.RunAsync(runaway, client, tools, "Keep going.", maxIterations: 4));

static void Report(LoopResult result)
{
    foreach (var message in result.Messages)
    {
        foreach (var block in message.Content)
        {
            var line = block switch
            {
                TextBlock t => t.Text,
                ToolUseBlock u => $"-> {u.Name}({string.Join(", ", u.Input.Select(kv => $"{kv.Key}={kv.Value}"))})",
                ToolResultBlock r => $"<- {(r.IsError ? "[error] " : "")}{Truncate(r.Content)}",
                _ => block.ToString() ?? "",
            };
            Console.WriteLine($"[{message.Role}] {line}");
        }
    }
    Console.WriteLine($"Stopped: {result.StopReason} after {result.Iterations} iterations.");
}

// One line per run: time, iterations, how many nudges it took, the first and final verdict, and whether the
// first verdict matched what compute_spread_stats says (the measure of the model's own judgement).
static string SummaryRow(int run, LoopResult result, (bool Alarm, double GapBps, double ToleranceBps)? expected, TimeSpan elapsed)
{
    var nudges = result.Messages.Count(m => m.Role == "user"
        && m.Content.OfType<TextBlock>().Any(t => t.Text.StartsWith("You have not finished")));
    var verdicts = result.Messages.Where(m => m.Role == "assistant")
        .Select(m => AlarmRecord.Parse(string.Join("\n", m.Content.OfType<TextBlock>().Select(t => t.Text)), ""))
        .Where(r => r != null).Select(r => r!.Alarm).ToList();
    string Name(bool? alarm) => alarm == null ? "-" : alarm.Value ? "ALARM" : "OK";
    var first = verdicts.Count > 0 ? verdicts[0] : (bool?)null;
    var final = verdicts.Count > 0 ? verdicts[^1] : (bool?)null;
    var right = expected is { } e && first != null ? (first == e.Alarm ? "yes" : "NO") : "-";
    return $"{run,3} {elapsed.TotalSeconds,5:F0}s {result.Iterations,5} {nudges,6} {Name(first),-6} {Name(final),-6} {right,-6} {(result.Incomplete ? "INCOMPLETE" : "no")}";
}

static string Truncate(string s) => s.Length <= 160 ? s : s[..160] + "...";
