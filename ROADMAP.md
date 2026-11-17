# Roadmap

**Status:** Phase 1 (simulator) is done. The plan now runs the loop on a local Ollama model and ends with a manual Claude Desktop run (see "Lessons from the flaky agent"). The model layer is ported (#51) and the MCP server scaffold with `get_history` and `get_benchmark` is built (#25). `compute_spread_stats` is written (#28). The hand-written agent loop runs end to end on a scripted model against the real server (#31). Next: 3.1b, prompt and live Qwen run (#56). Phase 1 and 2 parent issues (#13, #14) are closed; only deferred items remain open under them.

## Purpose

Build an agent that answers one operational question about prices a bank publishes,
prices that flow straight through to public venues and Bloomberg:

> **Is this published price a real market price, or is it wrong, and if wrong, why?**

Volatility alone is not the answer. A price can be far from where it was an hour ago
and still be correct. Mispricing is a defect in the pricing system: a dial or switch in
the quant library, a release that did not go out, a stale or broken feed, a scaling or
mapping error. The agent is scored against a simulator with known ground-truth causes.

It is a companion to the flaky-test agent. The pattern is the same: a hand-written
`call -> tool_use -> loop` cycle in C#, with an MCP server as tool producer and the agent
loop as tool orchestrator. This one is applied to a domain-specific problem tied to real
yield-curve pricing work.

### Intended narrative

"I built an agent that decides whether a published price is a real market price or a
pricing-system defect. It compares the price with an independent benchmark and with
the rest of the curve, calibrates per-node tolerances from history instead of using a
fixed threshold, and uses the shape of the time series and the change history to
attribute a cause. I measured it against a simulator with known causes. This maps
directly to monitoring the real-time prices a bank publishes."

"I built a yield curve anomaly/mispricing detection agent using a simulator with
known ground truth — it distinguishes genuine macro-driven curve moves from
mispriced ticks by reasoning across per-tenor volatility, whole-curve correlation,
curve-shape smoothness, and the pricing graph's own configuration state, not just
fixed thresholds. I measured precision/recall against the simulator's labels. This
maps directly to a real mispricing problem in real-time swap/yield-curve pricing."

## Problem framing

The agent asks two questions in order.

1. **Is it off-market?** Compare the published price with an independent reference
   (a benchmark, and the node's own history).
2. **If so, why?** Attribute a cause using the shape of the time series.

The MVP only answers the first question with an alarm. Cause labels come later. The
causes we eventually want to tell apart:

| Cause | Meaning | Series signature |
|---|---|---|
| Market move | Legitimate move | Price and benchmark move together; gap stays flat |
| Config dial | A pricing dial or switch changed | Step in the gap at one time, then a permanent offset |
| Missing release | A fix did not ship | Permanent offset; version mismatch |
| Stale feed | Input stopped updating | Price freezes while the benchmark keeps moving |
| Scaling bug | Unit or decimal error | Ratio to benchmark sits at a power of ten |
| Mapping fault | Duplicate or mis-mapped node | Node tracks the wrong neighbour, or two nodes identical |

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

## Requirements

What the real-world answers told us:

- **Benchmarks differ per node.** Nodes like 2Y, 5Y and 10Y anchor on Government of
  Canada rates. Others compare with a competitor bank or a market rate, which is weaker
  evidence. Some tenors, like 3W, may have no benchmark at all.
- **A price travels a pipeline:** Bloomberg, MarketFlow, MQ, PricingService, calc graph,
  calc graph configuration. Platform changes such as a .NET or quant-library upgrade can
  also move prices, and a wrong upstream source is a defect too.
- **We can see two points:** the wire price coming in and the published price going out.
  Nothing in between.
- **Change and release logs are hints only.** There is no one-to-one link between a code
  change and a price change, so the MVP does not use them.
- **History windows.** In practice 1 week to 1 month, sometimes 3 to 6 months, and up to a
  year for a manual sanity check.
- **Alarm fatigue is the central constraint.** There are 13 tenors across about 4
  curves, roughly 52 nodes. Three false alarms per check becomes about 156 per pass,
  which nobody reads. The targets are at most 20% of alarms false and at most 5% of real
  faults missed, with 10% as the hard limit. A miss is catastrophic for the engineer
  responsible, but traders usually catch it, so it must be rare. The targets will evolve.
- **Alerting is out of scope.** The agent emits an OpenTelemetry record. Grafana, Splunk
  or Elastic own the alarm rules, and the agent does not page anyone.

## How the agent decides

1. **Judge the gap, not the price.** A fixed threshold like 3.5003 cannot work because
   volatility moves prices legitimately. Compare the published price with a benchmark.
   Market moves hit both, so they cancel out, and what is left is small enough to
   threshold.
2. **Each node has its own normal.** The 10Y and 50Y differ in volatility, liquidity and
   benchmark quality, so there is no single number. Tolerance is a multiple of that
   node's own noise, learned from its history, with a floor in basis points.
3. **Persistence, grouping and budget.** The gap has to hold for several ticks. Nodes
   that share a cause become one alarm. The tolerance is set from the false-alarm budget,
   not the other way round.
4. **The second detector: the node's own history.** Take the node's prices over a window
   (1W, 1M, sometimes 3M or 6M) and ask whether the current value sits outside the
   middle 90% of that window, between the 5th and 95th percentile. Check both the level
   and the size of the move. Agreement across windows is a stronger signal. Percentiles
   are used because rates are not normally distributed.
   - It supports the main detector, and it is the **fallback** when no benchmark exists,
     for example a 3W tenor. The order is: benchmark detector with history support, or
     history alone when there is no benchmark.
   - It can raise an alarm on its own. If a node is an outlier against 3 months of its
     own history, something has very likely gone wrong, and the rare legitimate case is
     obvious to a human. Whether it may alarm alone is a setting, and the scorecard
     decides.
   - 90% means about 1 in 10 normal readings is outside the band, so the outlier must
     persist for several ticks. The confidence level and tick count are tuning knobs.
5. **Faults have shapes.** A step, a flat line, or a clean power-of-ten ratio looks
   different from a real move.
6. **The LLM reconciles evidence. It does not compute it.** The tools are deterministic
   and return numbers. The agent weighs them.

## MVP

A fake world, planted faults, and one question per node: alarm or no alarm.

- **Simulator:** one small curve with about a month of history, a benchmark series, and
  at least one node with no benchmark. Faults are planted at known times: a setting
  change first (stale price and unit error are deferred, #47). Genuine market moves are planted too and should
  stay quiet. The agent never sees the hidden labels.
- **Five tools, all deterministic:** `get_history`, `get_benchmark` (it says explicitly
  when none exists), `compute_spread_stats`, `compute_history_outlier`, `check_stale`.
- **Output:** a plain alarm record emitted as OpenTelemetry. It states which detector
  fired: benchmark, history, or both. A history-only alarm is weaker evidence and should
  read that way.
- **Scorecard:** at least 80% of alarms real, at most 5% of planted faults missed (10%
  hard limit), and better than a fixed-threshold baseline. We run it with and without
  "history can alarm alone". A large genuine macro move may trip the history outlier,
  and we count that as a false alarm, so the run will show how often it happens.
- **Left out on purpose:** the change log, wire price, cause labels, futures and swaps,
  several curves, web benchmarks, an Excel report.

## Phases

| Phase | What | API credits? | Original estimate |
|---|---|---|---|
| 0 | Requirements and MVP scope. Done. | No | n/a |
| 1 | Simulator: fake curve, benchmark series, planted faults, hidden labels, seeded. Tests project added. | No | 3-4 days |
| 2 | MCP server and the MVP tools, tried in MCP Inspector. | No | ~1 week |
| 3 | Agent loop (MCP client + hand-written loop behind `IModel`: scripted model for tests, local Ollama/Qwen for live runs), investigation prompt, fixed-threshold baseline, scoring. OpenTelemetry alarm is deferred. | No | ~1 week, plus 3-4 days scoring |
| 4 | **MVP finale:** a manual run in Claude Desktop against the same MCP server, scored against the same ground truth and compared with the Qwen runs. | No (Pro login) | 1 session |
| 5 | Calibration: per-node tolerance from history, robust statistics, false-alarm budget. | No | new |
| 6 | Futures and swaps curve: one unit, and the seam between them. | No | new |
| 7 | Shape and onset: butterflies, change points, release log, wire price, cause labels. | Light | was `check_mispricing` |
| 8 | Polish: confidence scores, optional web benchmark, report. | Light | 2-3 days |

No phase needs API credits. The loop runs on a free local model through Ollama
(`qwen2.5:3b`, then `7b` if it helps) and on scripted models, and Claude is used only by
hand in Claude Desktop on the Pro plan. A paid Claude API adapter is not built; the
`IModel` seam would let it be added later. Every phase is split into small tutorial-style
sessions: concept first, then the code written together. Each session follows issue,
branch, implementation, pull request.

The phase numbers were shifted by one from Phase 4 on (Nov 2026) to make room for the
Claude Desktop finale. Issues for those later phases do not exist yet.

## Lessons from the flaky agent

The sibling repo
[claude-mcp-flaky-test-agent](https://github.com/bleunguts/claude-mcp-flaky-test-agent)
built the same pattern and ran it on `qwen2.5:3b` and `7b` on this CPU-only 16 GB laptop.
What it learned becomes design rules here:

1. **Tool output must fit a small context.** Ollama runs at `num_ctx` 8192. `get_history`
   returns a bounded window with summary stats and a `Truncated` flag, never raw
   ticks. `get_history` returns 48 bucket averages whatever the window.
2. **Simple arguments.** Small models mangle numbers they copy. Tools take a tenor and a
   named window, not free-form ranges.
3. **A refusal is a normal result with an `Error` field**, not an exception. The loop also
   survives SDK-level failures (`IsError`).
4. **The server owns the boundary.** Seed and scenario come from server config, never the
   caller.
5. **Code does what code can.** Small models read figures well and turn them into a
   verdict badly. Tools return computed fields (gap in bps, tolerance, whether it is
   exceeded, ticks persisted). The loop checks the model's verdict against them and may
   state the measured figures itself.
6. **Guardrails live in the loop, not the prompt:** one tool per turn, required steps
   before an answer, a `Verdict:` line, a nudge limit, the iteration cap, an `INCOMPLETE`
   flag.
7. **Scripted model first**, including misbehaving scripts (runaway, unknown tool, bad
   argument), so the loop is proven with no LLM.
8. **Score with enough runs.** Three runs per case ranked nothing in the flaky repo. Use
   at least five per node and case, with a written rubric.
9. **Expect Qwen to prove the process, not the reasoning.** Phase 4 (Claude Desktop) is
   the reasoning check. A weak Qwen result is an acceptable outcome, and says the model is
   the limit, not the tools.
10. **Launch the built server dll**, not `dotnet run`: build output on stdout corrupts
    the protocol.

The model layer (messages, `IModel`, scripted and Ollama models) is copied from that repo
(#51); the loop itself is written here in 3.1.

## MVP session plan

A working end-to-end prototype matters more than extra features, so the MVP is a handful
of sessions, one or two per phase. Everything else is deferred.

| Phase | Session | Issue | Status |
|---|---|---|---|
| 1 | 1.6 Fault injector: setting change only (stale price and unit error deferred, #47) | #21 | done |
| 1 | 1.7 World builder: one call builds prices plus hidden ground truth, one fault per world, leakage test | #24 | done |
| 2 | 2.1 Server scaffold, `get_history`, `get_benchmark` (48 bucket averages per call) | #25 | done |
| 2 | 2.2 `compute_spread_stats` (main detector) | #28 | done |
| 3 | 3.0 Port the model layer from the flaky agent (copy, no loop) | #51 | done |
| 3 | 3.1 Agent loop and MCP client, proven on a scripted model | #31 | done |
| 3 | 3.1b Investigation prompt, JSON alarm record, live run on local Qwen | #56 | todo |
| 3 | 3.2 Fixed-threshold baseline and scorecard | #36 | todo |
| 4 | 4.1 Manual run in Claude Desktop, scored and compared with Qwen | new | todo |

Phase 1.1 to 1.5 (#16 to #20) are done.

**Deferred, open and labelled `deferred`:** `compute_history_outlier` (#29),
`check_stale` (#30), OpenTelemetry emission (#34), separate guardrails story (#37).
Until #29 lands, faults are planted on benchmarked nodes only, and the no-benchmark
node reports "unavailable" with no alarm. The alarm is a JSON record on the console
until #34. The leakage test lives in 1.7, and the agent is read-only because the tools
only read. Phases 5 to 8 above are the old Phases 4 to 7, unchanged.

The architecture diagram is in the [README](./README.md).

## Phase 1 design

Rule for the MVP: the simplest thing that works. Where unsure, hardcode and revisit.

- **Nodes:** 2Y, 5Y, 10Y (government-style benchmark), 30Y (competitor-style), 3W (no
  benchmark). Hardcoded list.
- **Time:** 1-minute ticks, 4-hour day (240 ticks), 14 days, 3,360 ticks per node. Hardcoded. Shrunk from 10 hours and 21 days in 2.1; longer history returns when the agent uses history to judge a price.
- **Three series per node:** a hidden true rate (one common factor plus node noise),
  the published price (truth plus small noise), and the benchmark (truth plus its own
  noise; competitor-style adds a few ticks of delay; none for 3W). Market moves change
  the truth, so published and benchmark move together. Faults change only the published
  price.
- **Faults:** a tiny per-node config (stale flag, scale factor, offset in bps) that the
  published price is computed through. A fault is a config change at a known time.
- **Scenarios:** genuine market move, stale price, unit error, setting change. About
  three faults a month, one at a time, no overlap.
- **Labels:** type, nodes, onset, duration, magnitude. Kept apart from the history the
  tools see; a leakage test checks that no public type exposes them.
- **Seed:** one seed drives all randomness, so a run repeats exactly.

## Later ideas

- **Butterflies** (e.g. 2x10Y - 5Y - 30Y) move far less than outright rates, so shape
  mispricing shows at a much smaller size.
- **Futures and swaps seam:** convexity adjustment, contract roll and day count are a
  bug-prone area. Futures quote as a price, so everything must be converted to bps of rate.
- **Wire price:** `published - wire` points at our pipeline, `wire - benchmark` points at
  the source. First addition after the MVP.
- **Interpolated benchmark** for a tenor like 3W, from the 1W and 1M benchmarks.
- **Change-point detection** to find when a gap began, then lining it up with release logs.
- **Web benchmark** as a weak signal. Public rates are delayed and quote different things.
- **Cause attribution** and an `unsure` verdict.
- **Contaminated history:** if a bug shipped three days ago, recent history is wrong too.
  Robust statistics help. Pre-break windows and the benchmark help more.

## Open design questions

- Real nodes and anchor types for the curve. The MVP uses the hardcoded set in Phase 1
  design; the user is still investigating the real ones.
- Whether the history outlier may alarm alone. Decided by the scorecard.
- The history confidence level and persistence tick count.
- Local OpenTelemetry viewer: a Grafana stack or the .NET Aspire Dashboard.
- Design of a `check_mispricing` decision tool (after the MVP).
- Report format, Excel or something else (after the MVP).
