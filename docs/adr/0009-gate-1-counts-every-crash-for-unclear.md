# ADR 0009: Gate 1's Unclear share counts every crash Sherlog shows

- **Status:** Accepted (owner decision)
- **Date:** 2026-11-26

Gate 1 allows no more than 40% of crashes to end up Unclear (plan.md, Phase 1). We had to decide which crashes that
share is taken over, because Windows logs many records that the person may never have noticed. On the dev PC, 41 of
44 crashes in the last 90 days came out Unclear, and about half of them were background "live kernel" reports
(codes 0x1a8 and 0x1b8) left by short display or driver hiccups.

Two rules were considered:

- **Count everything Sherlog shows.** Every crash in `sherlog crashes`, background live-kernel reports included.
- **Count only crashes a person would notice.** Leave background live-kernel reports out of the share, but keep listing
  them.

The owner chose to **count everything**. The share is the number of crashes in groups diagnosed Unclear, divided by all
crashes in all field bundles, which is what `sherlog eval` already reports as its Unclear share over crashes. The share
over groups is reported too, for context only; it is not a gate line.

Why: the gate should measure the list people actually see. If Sherlog shows a crash, it should be able to say something
useful about it. Leaving a class of records out would let the gate pass while the screen is still full of "Unclear".

Consequences:

- No code change: `Evaluator` already counts this way.
- The gate is strict on PCs with many background reports. If it fails for that reason, the fix is recipes that explain
  those reports honestly (for example live-kernel 0x1a8 and 0x1b8), or grouping that stops one hiccup from showing up
  as several crashes. Hiding records is not a fix.
- If a later phase wants to stop listing background reports by default, that change needs its own ADR, and the gate
  would then follow the new list.
