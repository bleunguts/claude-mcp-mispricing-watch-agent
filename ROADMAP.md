# Roadmap

## Purpose

Build an agent that flags mispriced/bad rate ticks across a simulated yield curve
(2y–30y tenors) by reasoning over per-tenor volatility, whole-curve behavior, and
pricing-graph state — not fixed thresholds. Scored against a simulator's known
ground-truth labels (precision/recall/F1).

Companion project to the flaky-test agent: same underlying pattern (Agent SDK,
explicit `call → tool_use → loop` cycle in C#, MCP server as tool producer / agent
loop as tool orchestrator), applied to a domain-credible problem instead of a
generically technical one.

## Domain clarification: mispricing vs. volatility

A curve can be volatile, jagged, or move a lot and still be a correct reflection of
the market (a rates shock, a real macro event). Mispricing is a different thing: a
misconfiguration or bug in the pricing graph itself — for example:

- a node stuck on a stale/cached rate while the rest of the curve updates
- an interpolation or calibration setting applied to the wrong tenor/bucket
- a decimal/unit scaling error introduced at the config level
- a duplicate or mis-mapped node

This means the simulator's "bad tick" category should specifically simulate
pricing-graph misconfiguration, not just generic statistical anomalies — and that
the tools split into two kinds: **evidence-gathering** (volatility, correlation,
smoothness — describe what the data looks like) and the **decision point**
(`check_mispricing` — checks pricing-graph config/state against known failure
signatures). Exact design of `check_mispricing` is still open — see "Open design
questions" below; this is deliberately left unresolved for now so the repo
structure can go up first.

## Simulator design (CurveLab)

Generate synthetic yield curve ticks (timestamped snapshots: tenor, rate,
timestamp) with three scenario categories, each carrying a hidden ground-truth
label for later scoring:

1. **Normal market noise** — small per-tenor moves within realistic historical vol
   (e.g. 10y ~1–3bps/tick, 2y less, 30y more)
2. **Genuine macro event** — a real, correlated parallel-ish shift across the whole
   curve (simulated "Fed announcement" tick) — can be large/volatile and still
   correct
3. **Mispricing (pricing-graph fault)** — injected config-level faults: stale node,
   misapplied interpolation/calibration, decimal/unit scaling bug, duplicate/mis-
   mapped node — *not* a trader fat-finger input

## Agent workflow

1. Ingest rate stream from simulator (tenor, rate, timestamp)
2. Compute per-tenor stats: rolling mean/stddev, typical move size per tenor
3. Flag candidates:
   - Single-tenor move > N std devs from its own historical vol
   - Whole-curve parallel shift (all tenors moved together at same timestamp)
   - Curve shape breaks (spline-fit residual outliers)
4. Investigate each flagged point:
   - Check pricing-graph config/state for known failure signatures
   - Cross-reference known market events (optional macro-calendar tool)
   - Check if isolated to one source vs confirmed by a second
5. Classify: "likely real move" / "likely mispricing" / "needs human review"
6. Generate Excel report: flagged points, reasoning, before/after curve chart

## Tools (MCP server, tool producer)

| Tool | Role | Status |
|---|---|---|
| `get_recent_snapshots(tenor, n)` | historical window for vol calc | planned |
| `compute_tenor_volatility(tenor)` | rolling stddev per tenor | planned |
| `check_parallel_shift(snapshot)` | correlation across tenors at same timestamp | planned |
| `check_curve_smoothness(snapshot)` | spline fit, residual outlier detection | planned |
| `check_mispricing(snapshot)` | pricing-graph config/state consistency check — the actual decision point | **design TBD** |
| `check_market_events(timestamp)` | optional, cross-reference macro calendar | planned |
| `generate_report(flags)` | Excel with chart + reasoning column (ClosedXML/EPPlus) | planned |

## Open design questions

- `check_mispricing`: exact signature, what pricing-graph config/state it checks
  against, and whether it's a single tool or several — deliberately deferred,
  revisit before Phase 2
- Whether `check_market_events` is worth building or cut for scope
- Report format details (Excel vs. something else)

**Status:** Session 1 (scaffold) complete. Phase 1 (CurveLab simulator) next. Tests project is added in Phase 1.

## Phased plan

| Phase | Content | Needs API credits? |
|---|---|---|
| 1 (3–4 days) | Build CurveLab simulator — realistic base vol per tenor, random walk for normal conditions, inject the 3 scenario types at random intervals with hidden labels, output as CSV/in-memory stream | No |
| 2 (~1 week) | MCP server scaffold + evidence-gathering tools (`get_recent_snapshots`, `compute_tenor_volatility`, `check_parallel_shift`, `check_curve_smoothness`), tested in MCP Inspector | No |
| 3 | Design + implement `check_mispricing` once the open design question above is resolved | No |
| 4 (~1 week) | Agent core: hand-coded agentic loop (Anthropic C# SDK + MCP client) consuming the tools above, investigation prompt, first classifications scored against ground truth | Yes |
| 5 (3–4 days) | Scoring + reporting — precision/recall/F1 against simulator labels, Excel report with curve chart | Yes |
| 6 (2–3 days) | Polish — tune false-positive rate, add confidence score rather than binary flag | Light |

Sessions/phases that touch simulator, MCP server scaffold, and tool definitions
need no API credits and can run on a Claude Pro login, same pattern as Module 1.

## Interview narrative

"I built a yield curve anomaly/mispricing detection agent using a simulator with
known ground truth — it distinguishes genuine macro-driven curve moves from
mispriced ticks by reasoning across per-tenor volatility, whole-curve correlation,
curve-shape smoothness, and the pricing graph's own configuration state, not just
fixed thresholds. I measured precision/recall against the simulator's labels. This
maps directly to a real mispricing problem in real-time swap/yield-curve pricing."

## Relationship to the flaky-test agent

Same underlying pattern (Agent SDK, explicit `call → tool_use → loop` cycle in C#,
MCP server as tool producer / agent loop as tool orchestrator) applied to a
different, more domain-specific problem. The flaky-test agent is the generically
technical proof point; this one is the domain-credible one tied to real swap/yield
curve pricing work.
