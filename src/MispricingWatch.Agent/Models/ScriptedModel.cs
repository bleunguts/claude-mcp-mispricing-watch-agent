// Copied from claude-mcp-flaky-test-agent @ 99e77b6, ScriptedModel from src/FlakyDetective.Agent/ScriptedModel.cs
// and Script.cs (#51). Changes: namespace; IModel split out; stray MediaTypeNames using dropped; Script helpers
// kept in this file.
namespace MispricingWatch.Agent;

// Replays a fixed sequence of replies. Each step can read the history, for example to take an argument
// from the previous tool_result.
public class ScriptedModel(IEnumerable<Func<List<Message>, ModelResponse>> steps) : IModel
{
    private readonly Queue<Func<List<Message>, ModelResponse>> _steps = new(steps);

    public Task<ModelResponse> CompleteAsync(
        List<Message> messages,
        IReadOnlyList<ToolDefinition> tools,
        string? systemPrompt = null,
        CancellationToken cancellationToken = default)
    {
        if (_steps.Count == 0)
        {
            throw new InvalidOperationException("The script has run out of steps.");
        }
        var nextStep = _steps.Dequeue();
        return Task.FromResult(nextStep(messages));
    }
}

public static class Script
{
    public static ModelResponse CallTool(string id, string name, Dictionary<string, object?> input) =>
        new([new ToolUseBlock(id, name, input)], StopReason.ToolUse);

    // Must be EndTurn: with ToolUse the loop asks the model again and the script runs out of steps.
    public static ModelResponse Finish(string text) => new([new TextBlock(text)], StopReason.EndTurn);
}
