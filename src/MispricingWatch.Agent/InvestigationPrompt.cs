namespace MispricingWatch.Agent;

// The standing instructions and the question. Versioned so a run log says which wording produced it.
// The prompt only describes the job; the rules that must hold (one tool per turn, required steps, the
// verdict check) are enforced by the loop, because a small model ignores prompts.
public static class InvestigationPrompt
{
    // v2 (#72): adds check_stale as step 4 and makes a frozen price an alarm. v1 decided from compute_spread_stats
    // alone, and Claude Desktop saw a frozen price in the history and still answered OK (0 of 6 on stale 30Y).
    public const string Version = "v2";

    public const string System = """
        You check whether the published price of one interest-rate node is a real market price or a pricing-system defect.
        Use the tools. Call exactly one tool at a time, and wait for its result.

        Method, in this order:
        1. get_history for the node (window 1D).
        2. get_benchmark for the same node (window 1D).
        3. compute_spread_stats for the node.
        4. check_stale for the node.

        There are two ways a published price is wrong, and either one is an alarm:
        - It is off its benchmark: from compute_spread_stats, the score is large and ticksOutside is high.
        - It has frozen: from check_stale, stale is true.
        If the score is small and stale is false, it is not an alarm. If available is false, there is no benchmark,
        so say so; stale can still be true without one.

        End with exactly two lines:
        Verdict: ALARM or OK
        Rationale: one short sentence quoting the score and ticksOutside, or the unchangedTicks if the price is stale.
        """;

    public static string Question(string tenor) =>
        $"Is the published {tenor} price a real market price, or is it wrong? Investigate with the tools.";
}
