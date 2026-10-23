<div align="center">

# Mispricing Watch

### Is this published price real, or is our pricing system wrong?

![Phase](https://img.shields.io/badge/next-phase%201%20simulator-ee4fa0?style=for-the-badge)
![MVP](https://img.shields.io/badge/MVP-v3%20scoped-1fbf75?style=for-the-badge)
![.NET](https://img.shields.io/badge/.NET-10-8b5cf6?style=for-the-badge)
![MCP](https://img.shields.io/badge/MCP-tool%20producer-3b82f6?style=for-the-badge)
![Code](https://img.shields.io/badge/simulator%20code-none%20yet%2C%20by%20design-ffb020?style=for-the-badge)

</div>

A C# agent built as an MCP server (tool producer) plus a hand-written agent loop (tool orchestrator). Companion to [claude-mcp-flaky-test-agent](https://github.com/bleunguts/claude-mcp-flaky-test-agent), applied to yield-curve pricing. Built as an incremental tutorial: concept first, then code, one small session at a time.

---

<p align="center">
  <img src="docs/img/overview.png" alt="Mispricing Watch overview. The problem: a wrong rate goes public fast, and the agent raises an alarm only when the pricing system itself looks broken. A price travels Bloomberg, MarketFlow, MQ, PricingService, calc graph and config, and only the wire and published prices are visible. Six rules: judge the gap to a benchmark, each node has its own normal, persistence, one cause one alarm, budget first, and a second opinion from the node's own history. Faults look like a step, a flat line or a power-of-ten jump, while market moves keep the gap flat. Targets: at most 20 percent of alarms false and at most 5 percent of real faults missed. The MVP is a fake world with planted faults, five tools, an agent loop, an OpenTelemetry alarm and a scorecard. Next is Phase 1, the simulator." width="100%">
</p>

<sub>The image is rendered from [`docs/overview.html`](./docs/overview.html). Plan, requirements and open questions are in [`ROADMAP.md`](./ROADMAP.md).</sub>

## 📚 Docs

| File | What is in it |
|---|---|
| [`ROADMAP.md`](./ROADMAP.md) | Purpose, intended narrative, requirements, how the agent decides, MVP scope, phases, open questions. |
| [`CLAUDE.md`](./CLAUDE.md) | Working rules and session log. |

Source layout: `src/MispricingWatch.CurveLab` (simulator library), `src/MispricingWatch.McpServer` (tool producer), `src/MispricingWatch.Agent` (tool orchestrator). All currently empty stubs with TODO comments.
