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

## Domain clarification: mispricing vs. volatility (original, retained)

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

## Simulator design (CurveLab, original, retained)

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

> **Session 2 extension:** the MVP simulator additionally generates multi-day history per node, an independent benchmark series with its own noise, and a change log, and faults start at a known time mid-history. "Genuine macro event" maps to the `market_move` label; the pricing-graph fault types map to the labels in the table above.

## Agent workflow (original, retained)

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

> **Session 2 note:** step 3 (flag candidates) now also compares against the benchmark and neighbouring nodes using per-node calibrated tolerances instead of a fixed N std devs; step 4 adds change-log correlation; step 6 (Excel report) is post-MVP.

## Full tool set (original, retained; MVP subset marked below)

| Tool | Role | Status |
|---|---|---|
| `get_recent_snapshots(tenor, n)` | historical window for vol calc | planned |
| `compute_tenor_volatility(tenor)` | rolling stddev per tenor | planned |
| `check_parallel_shift(snapshot)` | correlation across tenors at same timestamp | planned |
| `check_curve_smoothness(snapshot)` | spline fit, residual outlier detection | planned |
| `check_mispricing(snapshot)` | pricing-graph config/state consistency check — the actual decision point | **design TBD** |
| `check_market_events(timestamp)` | optional, cross-reference macro calendar | planned |
| `generate_report(flags)` | Excel with chart + reasoning column (ClosedXML/EPPlus) | planned |

> **Mapping to the MVP:** `get_recent_snapshots` -> `get_history`; `compute_tenor_volatility` -> `compute_spread_stats` (calibrated sigma + standardised score); new in MVP: `get_benchmark`, `check_stale`, `get_change_log`. `check_parallel_shift`, `check_curve_smoothness` (-> butterflies / neighbour residuals), `check_mispricing` (design still TBD), `check_market_events` and `generate_report` are post-MVP.

## Requirements findings (Session 3, additive)

From the first round of real-world answers (details in `docs/REQUIREMENTS.md`):

11. **Alarm fatigue is the central constraint.** About 13 tenors x 4 curves = ~52 nodes;
    a few false alarms per node check becomes ~150 per pass and the human stops
    looking. Alert on **incidents** (group nodes that share a cause), require
    **persistence**, rank by **severity**, and calibrate thresholds from an
    **alarms-per-day budget** rather than from a sigma rule.
12. **Benchmarks differ per node.** Some nodes anchor on govt rates (e.g. GoC for
    2Y/5Y/10Y); others compare with a competitor bank or a market rate. Each node has an
    anchor type and a trust level; competitor/scraped rates are weak evidence.
13. **The cause space is a pipeline.** `Bloomberg -> MarketFlow -> MQ -> PricingService
    -> calc graph -> calc graph config`, plus platform/infra changes (.NET or quant
    library upgrades). "Why" has a second dimension: **which stage**. Post-MVP idea:
    add a pipeline-stage label to each fault and, if per-hop values are observable,
    localise by where the divergence first appears.
14. **A wrong upstream source is a defect** even when nothing in our code changed.
15. **Change and release logs are indicative only.** No one-to-one mapping between a
    code change and a price change, so `get_change_log` returns candidates near the
    onset with low trust.
16. **History window is 1 month up to 1 year** in the manual method, so the simulator
    and calibration should support months of history, not only days.

17. **Alerting is out of scope; emit OpenTelemetry-shaped incident records.** Alarm rules
    live in Grafana/Splunk/Elastic. The agent's output is a structured record
    (verdict, cause, evidence, severity, affected nodes). Wiring is post-MVP.
18. **Two observable points: wire price and published price.** No per-hop values.
    `published - wire` isolates our pipeline; `wire - benchmark` isolates the source.
19. **Targets:** < 20% false alarms; miss rate 5% target, 10% max (misses are
    catastrophic but traders usually catch them, so they must be rare). Expected to
    evolve.

Plain-language MVP and a draft v2 are in `docs/REQUIREMENTS.md` section 6; v1 below is
unchanged.

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

> **Session 3 update (MVP v3, see `docs/REQUIREMENTS.md` section 6):** the MVP is
> leaner than described above: the change log tool is dropped, output is a binary alarm
> emitted as an OpenTelemetry record (no cause attribution yet), and the node/anchor mix
> is left open pending the user's investigation. Scorecard: >= 80% of alarms real,
> <= 5% of planted faults missed (10% limit), beats a fixed-threshold baseline. Wire
> price (`published - wire`) is the first post-MVP addition. v1 above is kept as-is.

## Phases (revised in Session 2)

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

## Original phased plan (retained for reference)

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

> **Mapping:** original Phase 1 (simulator, 3-4 days) -> new Phase 1 (MVP simulator); Phase 2 (evidence tools, ~1 week) -> new Phase 2; Phase 3 (`check_mispricing`) -> Phase 6; Phase 4 (agent, ~1 week) -> new Phase 3; Phase 5 (scoring + report, 3-4 days) -> new Phase 3 scoring + Phase 7 report; Phase 6 (polish, 2-3 days) -> new Phase 7. New Phases 0, 4 and 5 are additions from the Session 2 design discussion.

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
- Session 3: which anchor types and nodes the MVP curve uses is left open for the user to investigate (v2's govt-plus-competitor mix is a placeholder). Local OpenTelemetry viewer (Grafana stack vs .NET Aspire Dashboard) to be chosen in the agent phase.
- Session 3: is the price observable at each pipeline hop, or only the final published price? Who receives the alarm, and what is the false-alarm budget in numbers? Which costs more, a false alarm or a miss?
- Original (retained): `check_mispricing` exact signature, what pricing-graph config/state it checks against, and whether it is a single tool or several; whether `check_market_events` is worth building or cut for scope; report format details (Excel vs. something else).

## Interview narrative

"I built an agent that decides whether a published price is a real market price or a
pricing-system defect. It compares the price with an independent benchmark and with
the rest of the curve, calibrates per-node tolerances from history instead of using a
fixed threshold, and uses the shape of the time series and the change history to
attribute a cause. I measured it against a simulator with known causes. This maps
directly to monitoring the real-time prices a bank publishes."

### Original narrative (retained)

"I built a yield curve anomaly/mispricing detection agent using a simulator with
known ground truth — it distinguishes genuine macro-driven curve moves from
mispriced ticks by reasoning across per-tenor volatility, whole-curve correlation,
curve-shape smoothness, and the pricing graph's own configuration state, not just
fixed thresholds. I measured precision/recall against the simulator's labels. This
maps directly to a real mispricing problem in real-time swap/yield-curve pricing."

## Relationship to the flaky-test agent

Same underlying pattern (Agent SDK, explicit `call -> tool_use -> loop` cycle in C#,
MCP server as tool producer / agent loop as tool orchestrator) applied to a
domain-specific problem tied to real yield-curve pricing work.
