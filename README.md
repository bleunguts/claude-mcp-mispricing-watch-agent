# claude-mcp-mispricing-watch-agent

An agent that answers one question about published prices: **is this a real market
price, or is the pricing system wrong, and if so, why?** Volatile or unusual moves can
still be correct; the agent looks for defects in the pricing system itself (a stale
node, a changed dial, a missing release, a scaling bug) by comparing against a
benchmark and the rest of the curve, with per-node tolerances learned from history
instead of one fixed threshold.

Built in C# with two halves that mirror a real agentic system:

- **MCP server** (tool producer) — deterministic tools that pull price history and
  benchmarks, compute calibrated spread statistics, and read change logs.
- **Agent loop** (tool orchestrator) — a hand-coded `call → tool_use → execute → loop`
  cycle (Anthropic C# SDK + MCP client) that reasons over the evidence those tools
  return and decides: real price, not real (and why), or unsure.

Companion project to [claude-mcp-flaky-test-agent](../claude-mcp-flaky-test-agent) —
same architecture pattern, applied to a domain-specific, quant-finance-relevant
problem instead of CI tooling.

## Status

🚧 Scaffold complete; **Phase 0 (requirements) is next**, then an MVP (swap-only
curve, three fault types). Implementation proceeds as an incremental tutorial. See
[`ROADMAP.md`](./ROADMAP.md) for the problem framing, design principles, MVP scope and
phases, and [`docs/REQUIREMENTS.md`](./docs/REQUIREMENTS.md) for the requirements draft.

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
│   [McpServerTool] history · benchmark · spread stats · stale check ·│
│   change log (MVP; more in later phases)                            │
│        │                                                             │
│        ▼                                                             │
│   src/MispricingWatch.CurveLab (simulator library, labeled data)    │
└───────────────────────────────────────────────────────────────────┘
```

## Getting started

See [`ROADMAP.md`](./ROADMAP.md). Start at Phase 0 (requirements), then the MVP simulator.
