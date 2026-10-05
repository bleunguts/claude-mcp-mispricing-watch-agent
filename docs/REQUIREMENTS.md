# Requirements (Phase 0 - draft)

Working document. Seeded from the Session 2 design discussion; to be completed with
real-world pricing knowledge before any simulator code is written. Session 3 added the
first round of real-world answers (marked **Answer (Session 3)**). Original questions
are kept; nothing is removed.

## 1. The question the agent answers
Is this published price a real market price, or is it wrong, and if wrong, why?

## 2. Users and moments of use
TODO: who is alerted, when, and what do they do with the answer? (price-monitoring
desk, quant support, release managers?) What response time matters (the "within
30 seconds" benchmark check)?

**Answer (Session 3):** the goal is an **alarm for a human to check**, which is
explicitly fine. The agent does not need to fix anything or be certain. See section 4
for the real constraint: the alarm must be rare enough to stay usable.

Still open: who the human is (price-monitoring desk, quant support, release managers)
and the response time that matters.

## 3. Inputs available in practice
TODO: confirm for the real environment.
- Published price history per node (intraday to ~2 weeks): assumed available.
- Benchmark / second source: per node? per tenor? which sources?
- Neighbouring nodes on the same curve: assumed available.
- Change / config log: readable? how granular?
- Release / deployment log: readable?
- Feed status / timestamps: available?

**Answer (Session 3):**

- **History window is longer than assumed.** The manual method today is to dig through
  **1 month, or even 6 months to 1 year** of the rate and check the current value sits
  inside a plausible range. (Earlier assumption was a day to 2 weeks.) This is the
  same idea as per-node calibration from history, just over a longer window.
- **Benchmarks differ per node.** There is no single reference.
  - Example for a Canadian bank: nodes like **2Y, 5Y, 10Y are anchored on
    Government of Canada (GoC) rates**.
  - Other nodes may be compared with **another bank's published rate** (for a CIBC-like
    bank, a competitor such as RBC) or with **some market rate**.
  - Competitor rates would come from the internet, so they are delayed, differently
    quoted and noisy. They are weak evidence compared with a govt anchor.
  - Consequence: every node carries an **anchor type** (govt benchmark, competitor,
    market rate) and a **trust level**, and the agent must weigh evidence accordingly.
- **Change and release logs are indicative, not a lookup.** Git logs and release notes
  may hint at a cause, but there is **no one-to-one relationship between a code change
  and an instrument's price**. Infrastructure and platform changes (a .NET upgrade, a
  quant-library upgrade, localised to the yield curve or platform-wide) can also move
  prices. So `get_change_log` returns **candidate** changes near the onset time, with
  low trust, not a verdict.
- **The source itself can be wrong.** A bad price from the upstream source is a defect
  even though nothing in our code changed.
- **The cause space is a pipeline, not a single graph.** A price travels:

  `Bloomberg -> MarketFlow service -> MQ -> PricingService -> calc graph -> calc graph configuration`

  A fault can enter at any hop (plus platform or infrastructure changes underneath).
  So "why" has a second dimension: **which stage of the pipeline** introduced it.

Still open: is the value observable at each hop (so a divergence can be localised to
a stage), or only the final published price? Is a feed timestamp available per hop?

## 4. Decision taxonomy
Verdict: `real` / `not_real` / `unsure`. Causes: see ROADMAP.md ("Problem framing").
TODO: what does each verdict trigger operationally? How costly is a false alarm
versus a missed mispricing?

**Answer (Session 3): alarm fatigue is the central requirement.**

- A false alarm is common and acceptable **in isolation**. The problem is volume. The
  curve has 13 tenors (1W, 1M, 2M, 3M, 6M, 1Y, 2Y, 5Y, 10Y, 15Y, 30Y, 40Y, 50Y) and
  there are about **4 curves**, so roughly **52 nodes**. If one price check produces
  3 false alarms, a full pass produces on the order of **3 x 52 = 156**. That is
  unusable, and the human stops looking.
- Illustrative arithmetic (assumed cadence, for sizing only): checking every minute
  across a 10-hour day is about 600 checks x 52 nodes = ~31,000 node-checks per day.
  Allowing at most about **one false alarm per day** across everything means a
  per-node-check false-positive rate near **3 in 100,000**. A naive "3 sigma on each
  tick" rule is several orders of magnitude too noisy.
- Design implications (recorded as principles in ROADMAP.md):
  1. **Alert on incidents, not nodes.** If one upstream cause moves ten nodes, raise
     **one** alarm that groups them, not ten.
  2. **Persistence before alarm.** A reading must hold for several ticks, or pass a
     CUSUM-style test, before it counts.
  3. **Severity and ranking.** Distinguish "look now" from "look when convenient", so
     `unsure` does not page anyone.
  4. **Budget-first calibration.** Pick the false-alarm budget (alarms per day across
     the whole curve set) and derive thresholds from it, not the other way round.
- Which costs more, a false alarm or a missed mispricing? Not stated yet; the answer
  sets how aggressive the budget can be. Open.

## 5. Tolerances and calibration
Approach agreed in principle (ROADMAP.md, design principles 1-4). TODO: realistic
spread tolerances per tenor and instrument type (futures vs swaps); false-alarm
budget the desk can live with.

**Answer (Session 3):** the legitimate gap **depends on the instrument anchored on the
curve**, and there is no single straight answer. That confirms the design principle
that tolerance must be **learned per node from that node's own history**, not set as
one number, and that the node's anchor type matters (a spread to a govt benchmark is
tighter than a spread to a scraped competitor rate). Concrete per-tenor tolerances
are therefore a **simulator parameter we choose**, not a number we need from you.

Still open: the false-alarm budget in numbers (e.g. "no more than N per day"); see
section 4.

## 6. MVP scope sign-off
See "Minimum viable product" in ROADMAP.md. TODO: confirm in/out of scope and the
success criteria (beat a fixed-threshold baseline; explainable verdicts).

**Answer (Session 3):** not signed off yet; "lets iterate". The MVP is restated in plain
language below, with a draft v2 that reflects the answers above. v1 stays in ROADMAP.md.

### The MVP in plain language
A **fake world** we fully control: one small yield curve, a few months of price
history, and a stand-in for the outside benchmark. We **secretly plant a few kinds of
problems** at known times (a price that freezes, a price with a unit error, a pricing
setting that changes). We give the agent a handful of simple **tools** that fetch
history, compare with the benchmark, and report how unusual a node looks compared
with its own normal. The agent reads that evidence and **raises an alarm or stays
quiet**, and says what it suspects. We then check its answers against the secret
list: did it catch the planted problems, and how many false alarms did it raise?

That is all the MVP is: fake world, planted problems, a few tools, an agent that decides,
and a scorecard. Everything else in the roadmap is added after this works.

### Draft v2 (additions from Session 3, for discussion)
- **Two anchor types in the fake curve:** a few nodes (e.g. 2Y, 5Y, 10Y) compared with
  a tight govt-style benchmark, and one long node (e.g. 30Y) compared with a noisier,
  delayed competitor-style benchmark. This exercises the trust-level idea.
- **History of about a month** (longer than v1's few days), to match the manual
  "look back over history" method.
- **Change log is noisy:** the simulator emits several unrelated entries, so the agent
  must treat it as weak evidence (red herrings), not an answer key.
- **Success is measured on false alarms too:** target an explicit alarms-per-day budget
  across the whole curve, alongside catching the planted faults and beating a fixed
  threshold baseline.
- **Incident grouping is simulated lightly:** one planted upstream fault can affect
  several nodes at once, and should produce one alarm.
- **Deliberately not in the MVP:** pipeline-stage localisation (needs per-hop values we
  may not have), futures and the seam, multiple curves, web scraping for benchmarks.

## 7. Evaluation plan
Precision/recall and cause accuracy against simulator ground truth, compared with a
fixed-threshold baseline. TODO: define the scoring cases, including red herrings (a
benign config change coinciding with a genuine market move).

**Addition (Session 3):** also score **false alarms per day** against the budget, and
**incident count** (one grouped alarm per root cause, not one per node).

## 8. Open questions
Tracked in ROADMAP.md ("Open design questions"). New from Session 3:
- Who is the human receiving the alarm, and what response time matters?
- Is the price observable at each pipeline hop, or only at the end?
- The false-alarm budget in numbers.
- Which costs more: a false alarm or a missed mispricing?
- Do you agree with the draft v2 MVP above?
