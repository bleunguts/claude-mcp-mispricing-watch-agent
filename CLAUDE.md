# MispricingWatch - MCP + Agentic Mispricing Detector

## What this is
A C#/.NET agent that watches a simulated yield curve and flags pricing-graph mispricing (stale node, misapplied calibration, scaling bug), as opposed to genuine market moves. Two parts: an MCP server (tool **producer**) and a hand-coded agentic loop that consumes it as an MCP client (tool **orchestrator**). Companion to `claude-mcp-flaky-test-agent`.

The aim is to understand agentic architecture from the inside. Favour clear, explicit code whose design I can walk someone through, over clever abstractions.

## How to work with me
- This repo is a **tutorial built incrementally**. Work in bite-sized sessions: explain the concept briefly first, then build. One topic per session.
- **Do not check in implementation code ahead of the session that covers it.** Until then, `.cs` files are empty stubs with TODO comments. We work through the code and method together, step by step.
- Let me write or approve the key code, especially the agentic loop. Don't silently generate large chunks.
- Workflow per session: GitHub issue -> branch -> implementation -> PR. Each PR updates the status table in ROADMAP.md.
- At the end of each session, update the Session log below (one or two lines) and suggest a commit message.

## Architecture
```
src/MispricingWatch.Agent      -> tool ORCHESTRATOR: Anthropic C# SDK (Messages API) + MCP client, hand-written loop
src/MispricingWatch.McpServer  -> tool PRODUCER: ModelContextProtocol C# SDK, stdio transport, [McpServerTool] handlers
src/MispricingWatch.CurveLab   -> library: synthetic yield-curve simulator with hidden ground-truth labels
```
Tools are listed in ROADMAP.md. Tests project is deferred to Phase 1.

## Hard rules
- **Hand-code the loop.** Use `client.Messages.Create` and handle `tool_use` -> MCP `CallToolAsync` -> `tool_result` -> repeat until `end_turn`. No `IChatClient` + `UseFunctionInvocation()`.
- The MCP server contains **no LLM calls**. Deterministic C# only.
- stdio transport: **log to stderr only**. stdout is the protocol channel.
- **Ground-truth labels never reach the agent.** Tools must not leak the scenario label. Scoring answers live in a gitignored `GROUND_TRUTH.md`; don't read or reproduce it.
- Guardrails: the agent is read-only analysis, and caps loop iterations.

## Packages (added in the session that needs them)
- `Anthropic`: official C# SDK, v10+
- `ModelContextProtocol` (+ `Microsoft.Extensions.Hosting` for the server)
- Excel report library (ClosedXML or EPPlus) in Phase 5

## API access
Claude Code runs on my Pro login. The Agent (Phase 4+) calls the Claude API directly and needs a Console API key with credits in the `ANTHROPIC_API_KEY` env var, never in the repo.

## Plan
Status, phases and open design questions live in [ROADMAP.md](ROADMAP.md), the single source of truth.

## Session log
- Session 1 (complete): repo scaffold - solution, three empty projects (CurveLab library, McpServer, Agent), CLAUDE.md, .gitignore, settings. No implementation. Next: Phase 1 (CurveLab simulator design).
