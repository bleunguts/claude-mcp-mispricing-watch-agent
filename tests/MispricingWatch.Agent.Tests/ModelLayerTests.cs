namespace MispricingWatch.Agent.Tests;

public class ModelLayerTests
{
    private static List<Message> OneToolRoundTrip() =>
    [
        new("user", [new TextBlock("Check the 10Y node.")]),
        new("assistant", [new ToolUseBlock("call_1", "get_history", new() { ["tenor"] = "10Y" })]),
        new("user", [new ToolResultBlock("call_1", "{\"points\":3}", false)]),
    ];

    [Test]
    public void ToOllamaMessages_ToolResult_CarriesToolNameOfEarlierToolUse()
    {
        var messages = OllamaModel.ToOllamaMessages(OneToolRoundTrip(), null);

        var toolMessage = messages.Single(m => m.Role == "tool");
        Assert.That(toolMessage.ToolName, Is.EqualTo("get_history"));
        Assert.That(toolMessage.Content, Is.EqualTo("{\"points\":3}"));
    }

    [Test]
    public void ToOllamaMessages_SystemPrompt_IsFirstMessage()
    {
        var messages = OllamaModel.ToOllamaMessages(OneToolRoundTrip(), "You watch prices.");

        Assert.That(messages[0].Role, Is.EqualTo("system"));
        Assert.That(messages[0].Content, Is.EqualTo("You watch prices."));
    }

    [Test]
    public void ToModelResponse_ToolCall_StopReasonIsToolUse()
    {
        var reply = new OllamaMessage("assistant", "",
            [new OllamaToolCall("abc", new OllamaFunctionCall("get_benchmark", []))]);

        var response = OllamaModel.ToModelResponse(reply);

        Assert.That(response.StopReason, Is.EqualTo(StopReason.ToolUse));
        Assert.That(response.Content.OfType<ToolUseBlock>().Single().Id, Is.EqualTo("abc"));
    }

    [Test]
    public void ToModelResponse_ToolCallWithoutId_GeneratesOne()
    {
        var reply = new OllamaMessage("assistant", "",
            [new OllamaToolCall(null, new OllamaFunctionCall("get_benchmark", []))]);

        var toolUse = OllamaModel.ToModelResponse(reply).Content.OfType<ToolUseBlock>().Single();

        Assert.That(toolUse.Id, Does.StartWith("call_"));
    }

    [Test]
    public void ToModelResponse_PlainText_StopReasonIsEndTurn()
    {
        var response = OllamaModel.ToModelResponse(new OllamaMessage("assistant", "No alarm."));

        Assert.That(response.StopReason, Is.EqualTo(StopReason.EndTurn));
    }

    [Test]
    public async Task CompleteAsync_ScriptExhausted_Throws()
    {
        var model = new ScriptedModel([_ => Script.Finish("done")]);

        var first = await model.CompleteAsync([], []);

        Assert.That(first.StopReason, Is.EqualTo(StopReason.EndTurn));
        Assert.ThrowsAsync<InvalidOperationException>(() => model.CompleteAsync([], []));
    }
}
