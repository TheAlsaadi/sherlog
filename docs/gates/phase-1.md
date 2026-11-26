# Gate 1: Prove it (draft)

- **Status:** In progress. Seeded crashes and timing pass. The first set of public cases is scored and **fails** the
  accuracy line (56%, and only 17 of the 30 cases needed).
- **Waiting on:** a fresh case set (`gate-1` became a development set when ADR 0011 changed recipes from its misses),
  an owner decision on whether 30 cases stays the target, and a second real PC's bundle for the Unclear share.

## Gate checks

| Gate 1 line (`plan.md` §7, as changed by ADR 0010) | Result | Status |
|---|---|---|
| 100% of seeded crashes read and matched | 4 of 4 | Passed |
| ≥ 80% of Confirmed and Likely correct on ≥ 30 solved public cases | 5 of 9 correct (56%) on 17 cases | **Failing** |
| No more than 1 wrong Confirmed | 0 (public cases and field bundles) | Passing so far |
| No more than 40% of crashes Unclear, on ≥ 2 real PCs | 100% (24 of 24) on 1 PC; counted over every crash (ADR 0009) | Pending |
| `sherlog explain` under 5 s on a typical PC | Median 2.40 s (dev PC), 3.12 s (owner's laptop) | Passed |
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

A second PC (the owner's laptop: Windows 11 Home, build 26300, Intel Core Ultra 7, 15 GB) ran the field-test kit on
2026-11-27: 3.96, 3.70, 3.10, 3.10, 3.12 s, **median 3.12 s**, all exit 0. Also under 5 s.

## 3. Solved public cases (`gate-1`): 17 cases, 56%

Collected 2026-11-27 under ADR 0010 and its amendment (`tests/public-cases/README.md`). The 14 fixed searches
(standard and extended) gave 14 cases from about 230 threads. The 8 solved-tag searches gave 3 more from about
75 threads; Ten Forums and Eleven Forum refused automated reading, and no Reddit threads came up. About 1 thread in
18 qualified. Most threads end with a helper's suggestion and no word from the poster, or with the poster still
crashing. All labels were written before Sherlog was run on them.

`sherlog eval tests/public-cases/gate-1` (2026-11-27):

| Measure | Result |
|---|---|
| Cases | 17 (gate needs 30) |
| Confirmed or Likely correct | **5 of 9 (56%)**, gate needs 80% |
| By label strength | confirmed-by-fix 1 of 2; owner-reported 4 of 7 |
| Wrong Confirmed | 0 |
| Unclear (not a gate line here) | 13 of 22 problems |

### Misses

Confident and wrong (4):

| Case | What Sherlog said | What fixed it |
|---|---|---|
| pc-0001 | NVIDIA driver reset (`gpu-nvidia-driver-reset`, Likely) | A faulty RAM stick |
| pc-0012 | Graphics timeout blue screen (`gpu-video-tdr-failure`, Likely) | Underclocking the graphics card |
| pc-0013 | Hardware error blue screen (`bsod-whea-uncorrectable-error`, Likely) | Lowering the AMD card's clock |
| pc-0016 | Hardware error blue screen (`bsod-whea-uncorrectable-error`, Likely), one of four groups | Replacing the power supply |

Unclear (Sherlog didn't claim a cause): pc-0003 and pc-0015 (RAM), pc-0004 (an AMD driver version), pc-0005 (Explorer's
Quick Access cache), pc-0008 (a network driver), pc-0011 (Media Feature Pack missing on an N edition), pc-0016's other
three groups (power supply), and pc-0017 (Norton).

### What it shows

- **Sherlog's "Likely" graphics answers are too sure.** Half of its confident misses blame the graphics driver when
  the real cause was unstable hardware (RAM or clocks). A driver reset or timeout is the symptom; the driver is often
  not the cause.
- **A labeling question to settle before the next set.** pc-0013 and pc-0016 were labeled with the specific cause
  (GPU clocks, power supply), not with `bsod-whea-uncorrectable-error`, whose summary says only "a hardware problem".
  The labels are left as written, because changing them after scoring would bias the result. The next set's README
  should say whether a broad recipe counts as correct when the fix found the specific part.
- **Recipe changes made from these misses turn `gate-1` into a development set** (ADR 0010). The gate then needs a
  fresh set.

### After ADR 0011 (development set, not a gate result)

The five graphics-timeout recipes (NVIDIA, AMD and Intel resets, LiveKernelEvent 141/117 and 0x116) changed from Likely
to Unclear, and their fixes now cover overclocks, heat and memory. Re-running `eval` on `gate-1` gives **4 of 6 (67%)**,
0 wrong Confirmed, and 16 of 22 problems Unclear:

- **Now Unclear instead of wrong:** pc-0001 (RAM) and pc-0012 (GPU clocks).
- **Now Unclear instead of right:** pc-0002 (a real AMD driver fix). The record can't tell it apart from the others.
- **Still counted as wrong:** pc-0013 and pc-0016 on 0x124. Under ADR 0011's labeling convention they'd count as right,
  because that recipe's summary names their causes. The labels here stay as written.

`gate-1` is a development set from here on (`tests/public-cases/README.md`), so this isn't a gate result.

## 4. Field bundles: 1 received, 0 counting

To fill in as bundles are labeled: precision by label strength, wrong Confirmed, Unclear share (over crashes and over
groups), number of qualifying bundles, and every miss. Bundles stay outside the repo (`docs/field-test.md`); this file
describes them by category only.

`sherlog eval` so far: 1 bundle, **0 qualifying** (gate needs 10), 0 of 0 Confirmed or Likely, 0 wrong Confirmed,
**Unclear 24 of 24 crashes (100%)**, 14 of 14 groups.

### own-01: the owner's laptop, 2026-11-27

- **Collected by** double-clicking the kit. 24 crashes in 14 groups over 90 days; the bundle is 14.5 KB.
- **Name removal, checked on that PC:** the PC name, user name, profile path (`\`, `\\` and `/` forms), account SID and
  Microsoft account email were all **not found** anywhere in the bundle. The domain check was skipped (it was
  `WORKGROUP` or the PC name).
- **Owner answers:** a chat app crashes sometimes while working or browsing; nothing changed; nothing tried yet.
- **Labels:** all 14 groups are `unknown`. The answers don't name a cause, and no fix has been tried, so nothing counts
  toward accuracy yet. If the owner updates or reinstalls the chat app, a follow-up a week later could make its label
  `confirmed-by-fix`.
- **What was Unclear:**

  | What crashed | Groups | Crashes | Notes |
  |---|---|---|---|
  | PC-maker and Intel background helpers (power and thermal tuning, networking, device service, audio add-in) | 8 | 15 | Most crash in their own vendor DLL. One service alone has 3 groups and 10 crashes |
  | A .NET chat app | 2 | 2 | One unhandled .NET exception, one crash inside the .NET runtime |
  | Microsoft Store and Windows services | 3 | 6 | Store crashed in a module that had already been unloaded (`…dll_unloaded`); one Windows helper hung twice |
  | A virtual-machine app | 1 | 1 | A hang |

  6 groups matched a recipe, all of them Unclear by design: `runtime-dotnet-unhandled-exception` ×2,
  `windows-app-not-responding` ×2, `runtime-app-fail-fast` and `game-own-code-access-violation`. The other 8 matched none.

- **What it suggests (one bundle, so not acted on yet):** on laptops, the maker's background helpers can be most of
  the list. A recipe for "a background helper crashed in its own files" could explain them honestly, and
  `game-own-code-access-violation` talks about games even when the app is a background service. Wait for more bundles
  before writing recipes from this.
- **Fixed from this bundle:** question 1 listed the chat app twice with no way to tell the two apart. The choices now
  show when each one last happened.

## How Gate 1 counts Unclear (decided)

The owner decided on 2026-11-26 to **count every crash Sherlog shows**, background live-kernel reports included
(ADR 0009). The gate line uses `sherlog eval`'s Unclear share over crashes; the share over groups is shown for context
only.

This is strict. On the dev PC, **41 of 44 crashes** in the last 90 days came out Unclear, and about half of them are
Windows background "live kernel" reports (codes 0x1a8 and 0x1b8) left by short display or driver hiccups. If the field
test fails this line for that reason, the next step is recipes that explain those reports honestly, or grouping that
stops one hiccup from counting as several crashes. Hiding records is not a fix.

## Misses

None recorded yet.
