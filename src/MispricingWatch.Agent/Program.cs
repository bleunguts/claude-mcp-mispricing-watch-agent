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

static string Truncate(string s) => s.Length <= 160 ? s : s[..160] + "...";
