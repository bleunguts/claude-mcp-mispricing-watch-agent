// Copied from claude-mcp-flaky-test-agent @ 99e77b6, src/FlakyDetective.Agent/Messages.cs (#51).
// Changes: namespace; ThinkingBlock and RedactedThinkingBlock dropped (only an Anthropic adapter needs them).
using System.Text.Json;

namespace MispricingWatch.Agent;

public abstract record ContentBlock;
public record TextBlock(string Text) : ContentBlock;
public record ToolUseBlock(string Id, string Name, Dictionary<string, object?> Input) : ContentBlock;
public record ToolResultBlock(string ToolUseId, string Content, bool IsError) : ContentBlock;

public record Message(string Role, List<ContentBlock> Content);
public enum StopReason { EndTurn, ToolUse }
public record ModelResponse(List<ContentBlock> Content, StopReason StopReason);

public record ToolDefinition(
    string Name,
    string? Description,
    JsonElement InputSchema
);
