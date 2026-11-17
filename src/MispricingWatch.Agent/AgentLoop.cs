using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace MispricingWatch.Agent;

public enum LoopStopReason { Completed, IterationCap }

public record LoopResult(LoopStopReason StopReason, string FinalText, int Iterations, List<Message> Messages);

// The hand-written agentic loop. The model asks for tools, we run them through MCP, we feed the results
// back, and we decide when to stop. The model never executes anything.
public static class AgentLoop
{
    private const string SkippedMessage =
        "Skipped: call one tool at a time. Call this tool again after you have seen the result of the previous call.";

    public static async Task<LoopResult> RunAsync(
        IModel model,
        McpClient client,
        IReadOnlyList<ToolDefinition> tools,
        string userPrompt,
        int maxIterations,
        string? systemPrompt = null)
    {
        var messages = new List<Message> { new("user", [new TextBlock(userPrompt)]) };

        for (var iteration = 1; iteration <= maxIterations; iteration++)
        {
            var response = await model.CompleteAsync(messages, tools, systemPrompt);
            messages.Add(new Message("assistant", response.Content));

            var toolUses = response.Content.OfType<ToolUseBlock>().ToList();
            if (response.StopReason != StopReason.ToolUse || toolUses.Count == 0)
            {
                var text = string.Join("\n", response.Content.OfType<TextBlock>().Select(t => t.Text));
                return new LoopResult(LoopStopReason.Completed, text, iteration, messages);
            }

            // Every tool_use needs a tool_result. One tool per turn: only the first call runs, the rest
            // get an error result telling the model to call again.
            var toolResults = new List<ContentBlock>();
            foreach (var toolUse in toolUses)
            {
                if (toolResults.Count > 0)
                {
                    toolResults.Add(new ToolResultBlock(toolUse.Id, SkippedMessage, true));
                    continue;
                }

                try
                {
                    var toolResponse = await client.CallToolAsync(toolUse.Name, toolUse.Input);
                    var text = string.Join("\n", toolResponse.Content.OfType<TextContentBlock>().Select(c => c.Text));
                    toolResults.Add(new ToolResultBlock(toolUse.Id, text, toolResponse.IsError == true));
                }
                catch (Exception ex)
                {
                    // A failing tool is information for the model, not a crash of the loop.
                    toolResults.Add(new ToolResultBlock(toolUse.Id, $"Tool call failed. {ex.Message}", true));
                }
            }
            messages.Add(new Message("user", toolResults));
        }

        return new LoopResult(LoopStopReason.IterationCap, "", maxIterations, messages);
    }
}
