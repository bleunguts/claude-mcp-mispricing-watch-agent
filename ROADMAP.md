# Roadmap

**Status:** Session 1 (scaffold) complete. Session 2 (requirements + MVP scope) in progress. Next: Phase 0, requirements.

## Purpose

Build an agent that answers one operational question about prices a bank publishes
(and that flow straight through to public venues and Bloomberg):

> **Is this published price a real market price, or is it wrong, and if wrong, why?**

Volatility alone is not the answer. A price can be far from where it was an hour ago
and still be correct. Mispricing is a defect in the pricing system: a dial or switch in the quant
library, a release that did not go out, a stale or broken feed, a scaling or mapping
error. The agent is scored against a simulator with known ground-truth causes.

## Problem framing: two-stage diagnosis

1. **Is it off-market?** Compare the published price with an independent reference
   (a benchmark or second source, and neighbouring nodes on the same curve).
2. **If so, why?** Attribute a cause using the shape of the time series and the
   change history (config changes, releases, feed status).

Candidate causes (ground-truth labels):

| Label | Meaning | Series signature |
|---|---|---|
| `market_move` | Legitimate move | Price and benchmark move together; spread stays flat |
| `config_dial` | A pricing dial or switch changed | Step in the spread at one timestamp, then persistent offset; config log entry |
| `missing_release` | A fix did not ship | Persistent offset; version mismatch in the release log |
| `stale_feed` | Input stopped updating | Variance collapses while the benchmark keeps moving |
| `scaling_bug` | Unit/decimal error | Ratio to benchmark sits at a power of ten |
| `mapping_fault` | Duplicate or mis-mapped node | Node tracks the wrong neighbour, or two nodes identical |

## Design principles (from the design discussion)

1. **Threshold the surprise, not the price.** A fixed number like 3.5003 cannot work,
   because volatility moves prices legitimately. Measure how unusual a move is given
   recent volatility and time of day.
2. **Model the spread, not the price.** Price minus benchmark (or minus the value
   implied by neighbouring nodes) cancels market volatility, leaving a small, roughly
   stationary residual whose tolerance means something.
3. **No single threshold across the curve.** Tolerance is `k x sigma_node`, with
   `sigma_node` learned from that node's own history (robust statistics, e.g. MAD) and
   a floor in bps. The 10Y and the 50Y get different tolerances automatically. Only
   `k` and the floor are shared settings. Sparse nodes shrink toward their bucket
   (short / belly / long).
4. **"To what accuracy?" is answered empirically.** Choose `k` from a false-alarm
   budget (e.g. one a week across the curve) and measure precision/recall against
   simulator labels. Require persistence (N ticks or CUSUM), not a single tick.
5. **Faults have shapes.** Steps, flat-lines, constant power-of-ten ratios and
   identical nodes look different from real moves. Change-point detection on the
   spread gives the onset time, which is lined up against the change and release logs.
6. **History can be contaminated.** If a bug shipped three days ago, recent history is
   wrong too. Use robust statistics, pre-break windows where possible, and the
   independent benchmark.
7. **Compare shapes, not just levels.** Butterflies (e.g. 2x10Y - 5Y - 30Y) move far less
   than outrights, so shape mispricing is detectable at a much smaller size.
8. **Mixed instruments.** The curve uses bond futures at the short end and swaps at the
   long end. Convert to one unit (bps of rate) first. The futures-to-swaps seam
   (convexity adjustment, contract roll, day count) is its own bug-prone area.
9. **The LLM reconciles evidence, it does not compute it.** Tools are deterministic
   and return evidence (standardised scores, calibrated sigmas, change-point onset).
   The agent weighs the borderline cases, such as a real move coinciding with a config
   change.
10. **Web search as a benchmark is a weak signal.** Public sources are delayed and
    quote different things (mid vs bid, different curve construction). Treat as low
    trust, behind a proper reference feed. Post-MVP.

## Minimum viable product (MVP)

The thinnest slice that exercises the whole architecture (simulator, MCP tools,
hand-written agent loop, scoring) and answers the core question end to end.

**In scope**
- **Swap-only curve**, a handful of nodes (e.g. 2Y, 5Y, 10Y, 30Y, 50Y). No futures.
- **History:** several days of timestamped ticks per node, plus an independent
  benchmark series with its own noise.
- **Three causes + normal:** `market_move`, `stale_feed`, `scaling_bug`, and
  `config_dial` (with a change-log entry). Faults begin mid-history at a known time.
- **Tools (deterministic):** `get_history(node, window)`, `get_benchmark(node, window)`,
  `compute_spread_stats(node)` (calibrated sigma and standardised score),
  `check_stale(node)`, `get_change_log(since)`.
- **Agent output:** a structured verdict per flagged node: `real` / `not_real` /
  `unsure`, a cause label, and the evidence behind it.
- **Scoring:** verdict and cause accuracy, precision/recall against simulator labels.

**Out of scope for MVP (post-MVP phases below):** futures and the seam, butterflies,
change-point detection, release log / `missing_release`, `mapping_fault`, web
benchmark, Excel report, confidence scores.

**MVP success criteria (to confirm in Phase 0):** the agent beats a fixed-threshold
baseline on the same simulator data, and its verdicts are explainable from the
evidence it cites.

## Phases

| Phase | Content | API credits? | Status |
|---|---|---|---|
| 0 | **Requirements:** what the agent must answer, inputs available in practice, decision taxonomy, MVP scope sign-off, evaluation plan. Output: `docs/REQUIREMENTS.md` | No | Next |
| 1 | **MVP simulator (CurveLab):** swap-only nodes, per-node noise, multi-day history, benchmark series, 3 fault types + change log, seeded, hidden labels. Tests project added. | No | Planned |
| 2 | **MVP MCP server + tools** (list above), tested in MCP Inspector | No | Planned |
| 3 | **MVP agent loop:** hand-coded loop (Anthropic C# SDK + MCP client), investigation prompt, fixed-threshold baseline, scoring | Yes | Planned |
| 4 | **Calibration:** per-node sigma from history, robust stats, bucket shrinkage, false-alarm budget | No | Planned |
| 5 | **Futures + swaps curve:** instrument types, unit conversion, the seam and its fault (convexity adjustment) | No | Planned |
| 6 | **Shape and onset:** butterflies / neighbour residuals, change-point detection, release log + `missing_release`, `mapping_fault` | No / Light | Planned |
| 7 | **Polish:** confidence scores, optional web benchmark, report output | Light | Planned |

Phases 0, 1, 2, 4, 5 need no API credits and run on a Claude Pro login. Phases that call the
Claude API need a Console API key (`ANTHROPIC_API_KEY`, never committed).

Every phase is split into small tutorial-style sessions: concept first, then code written
together. Each session follows issue, branch, implementation, pull request.

## Architecture

```
┌──────────────────────── MispricingWatch.slnx ───────────────────────┐
│                                                                      │
│  MispricingWatch.Agent  (tool ORCHESTRATOR)                         │
│   ├─ AnthropicClient ── Messages API (hand-written loop)            │
│   └─ McpClient ──stdio──┐                                           │
│                         ▼                                           │
│  MispricingWatch.McpServer  (tool PRODUCER, deterministic, no LLM)  │
│   [McpServerTool] history · benchmark · spread stats · stale ·      │
│   change log (MVP); butterflies · change-point · release log (later)│
│        │                                                             │
│        ▼                                                             │
│   MispricingWatch.CurveLab (simulator library, hidden labels)       │
└──────────────────────────────────────────────────────────────────────┘
```

The agent never sees the ground-truth label. Tools must not leak it.

## Open design questions

- Phase 0: how tight is the legitimate gap between a published price and a benchmark in
  practice? This sets the simulator's benchmark-noise level.
- Phase 0: is there a readable change/release log in the real environment, or does
  someone have to dig for it?
- Phase 0: is there a benchmark for every node, including futures, or only some tenors?
- Representation of faults: do they alter published rates directly, or a small config
  object the rate depends on? (Leaning to a config object, so `check_mispricing`-style
  tools have something real to inspect.)
- Verdict format and how `unsure` is handled operationally (who gets alerted).
- Report format (Excel vs something else), deferred.

## Interview narrative

"I built an agent that decides whether a published price is a real market price or a
pricing-system defect. It compares the price with an independent benchmark and with
the rest of the curve, calibrates per-node tolerances from history instead of using a
fixed threshold, and uses the shape of the time series and the change history to
attribute a cause. I measured it against a simulator with known causes. This maps
directly to monitoring the real-time prices a bank publishes."

## Relationship to the flaky-test agent

Same underlying pattern (Agent SDK, explicit `call -> tool_use -> loop` cycle in C#,
MCP server as tool producer / agent loop as tool orchestrator) applied to a
domain-specific problem tied to real yield-curve pricing work.
