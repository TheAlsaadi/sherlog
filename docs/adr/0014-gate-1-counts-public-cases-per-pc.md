# ADR 0014: Gate 1's accuracy line counts each public case once

- **Status:** Accepted (owner decision)
- **Date:** 2026-11-27
- **Changes:** how ADR 0010 and ADR 0012's accuracy line is counted

## Context

`sherlog eval` scored public cases per problem (crash group). ADR 0013's mixed blue-screen rule answers every blue
screen in its window, so one PC with five different stop codes gives five confident answers, all right or all wrong
together. On the development set `gate-1b` the rule's 26 answers come from 7 PCs. Counted that way, three such PCs
on `gate-1c` could reach ADR 0012's minimum of 10 answers by themselves, and one wrong PC could sink the line. Neither
measures what the gate is for: how often Sherlog gives a person a right answer about their PC.

## Decision

The accuracy line counts **cases**. Each case is one PC and one solved thread.

- **A case is confident** when at least one of its problems got a Confirmed or Likely answer.
- **A confident case is correct** only when **every** Confirmed or Likely answer in it names the case's true cause. A
  person who is told two different confident things, and one of them is wrong, didn't get a right answer.
- **A case has a wrong Confirmed** when any of its Confirmed answers is wrong. It counts once toward Gate 1's "no more
  than 1 wrong Confirmed", alongside field bundles' wrong Confirmed problems.
- **The line:** at least **10 confident cases**, at least **80%** of them correct, on a frozen set of at least 30
  cases (ADR 0010, ADR 0012).
- `sherlog eval` prints the per-case figures as the gate line, and the per-problem figures below them as context.
  Field bundles are still scored per problem: one bundle holds unrelated problems, each labeled on its own (ADR 0006).

## Consequences

- On the development sets, per case: `gate-1` 3 of 5 confident cases correct, `gate-1b` 2 of 9. These use the labels
  as written, before ADR 0013's recipe existed, so they aren't evidence for the gate.
- A case with several problems where Sherlog says one thing confidently and leaves the rest Unclear counts as
  confident. Its result depends only on the confident answers.
- `gate-1c` is scored this way from the start.
