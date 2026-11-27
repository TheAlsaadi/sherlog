# ADR 0012: Gate 1's accuracy line needs at least 10 confident answers

- **Status:** Accepted (owner decision)
- **Date:** 2026-11-27
- **Changes:** Gate 1's accuracy line (ADR 0010); builds on ADR 0011

## Context

Gate 1's accuracy line is "at least 80% of Confirmed and Likely diagnoses are correct". It only looks at the answers
Sherlog is willing to give. ADR 0011 made Sherlog say Unclear unless the record narrows the cause, which is the honest
thing to do. The fresh frozen set `gate-1b` shows what that does to the line: 30 solved public cases, 54 problems,
**2 confident answers, both correct (100%)**, and 52 Unclear. The line passed, but it would have passed just the same
with 1 answer, or with 0 answers counted as "not applicable". A line that rewards staying quiet can't tell a careful
tool from a useless one.

## Decision

The accuracy line becomes: **at least 10 Confirmed or Likely diagnoses on one frozen set of at least 30 solved public
cases, and at least 80% of them correct.**

- Ten is the smallest number where one wrong answer (90% correct) still leaves the line meaningful, and where the
  80% bar means more than "4 of 5". It is about 1 confident answer for every 5 problems on `gate-1b`'s 54.
- The set must still be frozen, labeled before scoring, and collected with a fixed search list (ADR 0010).
- Nothing else in Gate 1 changes: no more than 1 wrong Confirmed, the Unclear share on real PCs (ADR 0009), seeded
  crashes and timing.

## Consequences

- **`gate-1b` does not pass the new line:** 2 confident answers, 10 needed. The result stays as recorded in
  `docs/gates/phase-1.md`; the line it is measured against changed.
- **Phase 1 has real work to do.** Sherlog needs to be able to say something confident and right more often, without
  going back on ADR 0011. The ways ADR 0011 named are the starting point: a rule that uses more of the record (for
  example resets together with blue screens of many different codes, which points to hardware), or the change timeline
  from Phase 2B. On `gate-1b`, 14 of 30 cases were fixed by a replaced part and 6 by a BIOS or tuning setting, so
  recipes that can only name drivers won't reach 10.
- **A new frozen set is needed.** If recipes change because of `gate-1b`'s misses, it becomes a development set (ADR
  0010) and the next gate run uses `gate-1c`, collected the same way. Collection yielded about 4 usable threads in 100
  (30 of about 730), so a fresh set means reading several hundred threads again, and any thread already used in `gate-1`
  or `gate-1b` is excluded.
- **`sherlog eval` still prints "gate needs 80%".** It should also print the minimum of 10 confident answers. That is a
  small change to the command and is not part of this ADR.
