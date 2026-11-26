# ADR 0010: Gate 1 measures accuracy on solved public cases

- **Status:** Accepted (owner decision)
- **Date:** 2026-11-27
- **Changes:** plan.md Phase 1 and Gate 1; extends ADR 0006 (labels) and ADR 0009 (Unclear share)

## Context

Gate 1 asked for evidence bundles from 10–20 friends' PCs, labeled with the true cause. The owner has no group of
friends to ask, so that source of real-world truth isn't available. Gate 1 still needs real crashes whose cause is
known, so Sherlog's answers can be scored.

Tech-support forums are full of them. In a solved thread, someone posts what Windows recorded (the app, the failing
file, the error code or the blue-screen stop code) and later says which fix stopped the crashes.

## Decision

Gate 1's accuracy lines are measured on a **frozen set of at least 30 solved public cases**. Field bundles from real
PCs (the owner's own PCs, plus any volunteers) are still collected and labeled. They are the only basis for the Unclear
share, and any labels they get are scored too.

### What a case is

One YAML file per solved thread in `tests/public-cases/<set>/` (format: `tests/public-cases/README.md`). It holds:

- **the thread's link**;
- **the crash facts** from the post, in the same fields a recipe matches on (source, app, faulting module, exception
  code, stop code, event name), plus how many times it happened and over how many days, when the post says;
- **the truth:** `true_cause` and `label_strength`, plus a one-line `evidence` note in our own words.

It holds nothing else: no user names, no quoted text, and no screenshots. Crash facts aren't creative work, and the
link credits the source.

### Which threads count

A thread is used only when all of these hold:

1. The post gives at least one recorded fact Sherlog reads: a faulting module, an exception code, a stop code or a
   live-kernel event name. "My game crashes" with no details is skipped.
2. The person who had the crash, not a helper, says which fix stopped it.
3. The fix is specific enough to name a cause (for example "rolled back the NVIDIA driver to 566.36"), not "reinstalled
   Windows and it went away".

`label_strength` follows ADR 0006:

- **`confirmed-by-fix`:** the poster says the crashes stayed gone for 7 days or more after the fix.
- **`owner-reported`:** the poster says the fix worked, with no time given or less than 7 days.

`true_cause` is the id of the recipe whose cause that fix addresses. When no recipe describes that cause, it is
`none:<short-name>` (for example `none:faulty-ram-stick`), which Sherlog can only get wrong or call Unclear.

### Collecting without cherry-picking

- Cases are found with a fixed list of searches, written in the set's README before collecting starts. The list
  covers each crash kind Sherlog reads: app crashes, hangs, graphics driver resets, blue screens, unexpected shutdowns,
  missing runtimes and live-kernel reports.
- For each search, results are taken in order, and every thread that meets the three rules above is kept. A thread is
  never skipped because of what Sherlog would say about it.
- The label is written from the thread **before** Sherlog is run on the case.

### Keeping the gate honest

- A set is **frozen** once it is merged. Its cases aren't edited afterwards, except to fix a copying mistake, and
  every such fix is listed in the gate file.
- If recipes are changed after looking at a frozen set's misses, that set becomes a **development set**. The next
  gate run needs a new frozen set of fresh cases (`gate-1b`, and so on). Otherwise the recipes would just be tuned to
  the test.

## Gate 1, as changed

- 100% of seeded crashes read and matched (unchanged).
- At least **80%** of Confirmed and Likely diagnoses correct on a frozen set of **at least 30 solved public cases**.
- **No more than 1** wrong Confirmed diagnosis, across public cases and field bundles.
- No more than **40%** of crashes Unclear, counted over every crash in field bundles from **at least 2 real PCs**
  (ADR 0009). Public cases don't count toward this share, because solved threads lean toward problems with known
  fixes.
- `sherlog explain` under 5 seconds on a typical PC (unchanged).
- Results and every miss written to `docs/gates/phase-1.md` (unchanged).

`sherlog eval` reads both kinds from one folder or several: `*.zip` evidence bundles and `*.yaml` public cases. It
reports them separately.

## Consequences

- No outside help is needed. Cases can be collected and checked by anyone from public links.
- Forum cases are biased toward problems that have known fixes, and they rarely show a PC's whole crash history. The
  Unclear share stays tied to real PCs for that reason, and the gate file has to say how few PCs it rests on.
- A case is a summary of a post, not raw records, so it doesn't test the readers. Seeded crashes and real bundles keep
  covering those.
- Volunteers stay welcome: any labeled field bundle is scored alongside the cases.
