# Gate 1: Prove it (draft)

- **Status:** In progress. Seeded crashes and timing are done; the field test hasn't started.
- **Waiting on:** the field test (issue #10, [field-test.md](../field-test.md)) and one owner decision (below).

## Gate checks

| Gate 1 line (`plan.md` §7) | Result | Status |
|---|---|---|
| 100% of seeded crashes read and matched | 4 of 4 | Passed |
| ≥ 80% of Confirmed and Likely correct on ≥ 10 labeled field bundles | Field test not started | Pending |
| No more than 1 wrong Confirmed | Field test not started | Pending |
| No more than 40% of crashes Unclear | Field test not started; see the open decision | Pending |
| `sherlog explain` under 5 s on a typical PC | Median 2.40 s | Passed |
| Results and misses written here | This file | In progress |

## 1. Seeded crashes: 4 of 4

Run in Windows Sandbox with `sandbox/Run-Seeded.ps1` on 2026-11-25 and 2026-11-26, using the programs CI builds
(artifact `seeded-programs`, see `sandbox/README.md`). Every seeded crash was read and matched to its expected cause.
GitHub's runners never log crash events, so this check runs in Sandbox, not on CI (ADR 0007 amendment).

| Seeded program | What it does | Expected record | Result |
|---|---|---|---|
| `SherlogSeedCrash.exe` | Crashes inside `SherlogSeedModule.dll` | Application Error 1000, module `SherlogSeedModule.dll`, `0xc0000005` | Matched |
| `SherlogSeedOverflow.exe` | Runs out of stack | Application Error 1000, `0xc00000fd` | Matched |
| `SherlogSeedFastFail.exe` | Ends itself with `__fastfail` | Application Error 1000, `0xc0000409` | Matched |
| `SherlogSeedNeedsRuntime.exe` | Needs the Visual C++ runtime, which the sandbox lacks | Application Popup 26 naming `VCRUNTIME140.dll`, recipe `runtime-vcredist-missing` | Matched |

Not covered yet: a seeded hang (Application Hang 1002); it's listed as deferred in `sandbox/README.md`.

## 2. Explain timing: median 2.40 s

`sherlog explain --latest`, 5 runs on the dev PC with output discarded (`Measure-Command`), 2026-11-26. Exit code 0
on every run.

| Build | Runs (s) | Median |
|---|---|---|
| Release build (`dotnet build -c Release`) | 2.70, 2.40, 2.38, 2.38, 2.40 | **2.40 s** |
| Field-test kit (self-contained single-file `sherlog.exe`) | 2.50, 2.39, 2.51, 2.52, 2.51 | **2.51 s** |

Both are under the 5 s limit. Earlier runs were 3.57 s (Task 8) and 2.24 s (Task 10, after error-report retries were
read once each).

## 3. Field bundles: pending

Not started. The kit (`sherlog-field-test` CI artifact) and both guides are ready. To fill in when bundles are
labeled: precision by label strength, wrong Confirmed, Unclear share (over crashes and over groups), number of
qualifying bundles, and every miss.

## Open owner decision: how Gate 1 counts Unclear

On the dev PC, **41 of 44 crashes** in the last 90 days came out Unclear. About half of them are Windows background
"live kernel" reports (codes 0x1a8 and 0x1b8): Windows records these for short display or driver hiccups, often with
no crash the person saw. Counted this way, the 40% Unclear limit is very hard to meet on any PC that logs them.

| Option | What counts toward the Unclear share |
|---|---|
| A. Everything Sherlog shows (current rule) | Every crash in the list, including background live-kernel reports |
| B. Only crashes a person would notice (recommended) | App crashes and hangs, blue screens, unexpected shutdowns, graphics resets, missing-runtime errors; background live-kernel reports are still listed but left out of the share |

Option B measures what the gate is for: whether Sherlog can explain the crashes people actually ask about. Option A
stays stricter, but it would mostly measure how chatty Windows' background reporting is.

**The gate stays at option A until the owner decides.**

## Misses

None recorded yet.
