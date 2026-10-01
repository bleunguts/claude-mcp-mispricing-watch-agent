# claude-mcp-mispricing-watch-agent

An agent that watches a simulated interest-rate yield curve and flags pricing-graph
mispricing — not just volatile or unusual market moves, but cases where the pricing
graph itself has gone wrong (a stale node, a misapplied calibration, a config-level
scaling bug).

Built in C# with two halves that mirror a real agentic system:

- **MCP server** (tool producer) — deterministic tools that pull curve data,
  compute statistics, and check pricing-graph state/config for known failure
  signatures.
- **Agent loop** (tool orchestrator) — a hand-coded `call → tool_use → execute → loop`
  cycle (Anthropic C# SDK + MCP client) that reasons over the evidence those tools
  return and decides: genuine market move, likely mispricing, or needs human review.

Companion project to [claude-mcp-flaky-test-agent](../claude-mcp-flaky-test-agent) —
same architecture pattern, applied to a domain-specific, quant-finance-relevant
problem instead of CI tooling.

## Status

🚧 Scaffold complete (Session 1): solution and empty projects build; no implementation yet.
Implementation proceeds as an incremental tutorial. See [`ROADMAP.md`](./ROADMAP.md) for the full spec, open design
questions, and the session-by-session plan.

## Why this is a hard problem

A yield curve can be volatile, jagged, or move a lot and still be *correctly
priced* — that's just the market. Mispricing is a different thing entirely: a bug
or misconfiguration in the pricing graph itself (a node stuck on a stale rate, an
interpolation setting applied to the wrong tenor, a decimal/unit scaling error, a
duplicate or mis-mapped node). Telling these apart — rather than flagging on a
fixed move-size threshold — is the actual problem this agent solves.

## Architecture

```
┌──────────────────────── MispricingWatch.slnx ────────────────────────┐
│                                                                      │
│  MispricingWatch.Agent  (tool ORCHESTRATOR)                         │
│   ├─ AnthropicClient ── Messages API (hand-written loop)            │
│   └─ McpClient ──stdio──┐                                           │
│                         ▼                                           │
│  MispricingWatch.McpServer  (tool PRODUCER)                         │
│   [McpServerTool] get_recent_snapshots · compute_tenor_volatility · │
│   check_parallel_shift · check_curve_smoothness · check_mispricing  │
│        │                                                             │
│        ▼                                                             │
│   src/MispricingWatch.CurveLab (simulator library, labeled data)    │
└───────────────────────────────────────────────────────────────────┘
```

## Getting started

See [`ROADMAP.md`](./ROADMAP.md) for the simulator design, tool specs, and the
phased build plan. Start at Phase 1 (simulator).
