# Requirements (Phase 0 - draft)

Working document. Seeded from the Session 2 design discussion; to be completed with
real-world pricing knowledge before any simulator code is written.

## 1. The question the agent answers
Is this published price a real market price, or is it wrong, and if wrong, why?

## 2. Users and moments of use
TODO: who is alerted, when, and what do they do with the answer? (price-monitoring
desk, quant support, release managers?) What response time matters (the "within
30 seconds" benchmark check)?

## 3. Inputs available in practice
TODO: confirm for the real environment.
- Published price history per node (intraday to ~2 weeks): assumed available.
- Benchmark / second source: per node? per tenor? which sources?
- Neighbouring nodes on the same curve: assumed available.
- Change / config log: readable? how granular?
- Release / deployment log: readable?
- Feed status / timestamps: available?

## 4. Decision taxonomy
Verdict: `real` / `not_real` / `unsure`. Causes: see ROADMAP.md ("Problem framing").
TODO: what does each verdict trigger operationally? How costly is a false alarm
versus a missed mispricing?

## 5. Tolerances and calibration
Approach agreed in principle (ROADMAP.md, design principles 1-4). TODO: realistic
spread tolerances per tenor and instrument type (futures vs swaps); false-alarm
budget the desk can live with.

## 6. MVP scope sign-off
See "Minimum viable product" in ROADMAP.md. TODO: confirm in/out of scope and the
success criteria (beat a fixed-threshold baseline; explainable verdicts).

## 7. Evaluation plan
Precision/recall and cause accuracy against simulator ground truth, compared with a
fixed-threshold baseline. TODO: define the scoring cases, including red herrings (a
benign config change coinciding with a genuine market move).

## 8. Open questions
Tracked in ROADMAP.md ("Open design questions").
