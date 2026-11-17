// Copied from claude-mcp-flaky-test-agent @ 99e77b6, IModel from src/FlakyDetective.Agent/ScriptedModel.cs (#51).
// Changes: namespace; moved to its own file.
namespace MispricingWatch.Agent;

// The seam the loop talks to: history and tools in, one response out. The loop cannot tell a script
// from a local model, which is what lets it be tested with no LLM.
public interface IModel
{
    Task<ModelResponse> CompleteAsync(
        List<Message> messages,
        IReadOnlyList<ToolDefinition> tools,
        string? systemPrompt = null,
        CancellationToken cancellationToken = default);
}
