# Gate 1: Prove it (draft)

- **Status:** In progress. Seeded crashes and timing pass. The fresh public set `gate-1b` (30 cases) is scored: 2 of 2
  confident answers correct, but Sherlog said Unclear for 52 of 54 problems. ADR 0012 now requires at least 10
  confident answers, so the accuracy line **does not pass yet**. The first set (`gate-1`, 56%) is a development set.
- **Waiting on:** recipes that can answer confidently and correctly more often (without undoing ADR 0011), then a fresh
  frozen set (`gate-1c`); and a second real PC's bundle for the Unclear share.

## Gate checks

| Gate 1 line (`plan.md` §7, as changed by ADR 0010) | Result | Status |
|---|---|---|
| 100% of seeded crashes read and matched | 4 of 4 | Passed |
| ≥ 10 Confirmed or Likely answers, ≥ 80% of them correct, on ≥ 30 solved public cases (ADR 0012) | `gate-1b` (frozen, 30 cases): **2 answers, 2 correct (100%)**. Only 2 of 54 problems got a confident answer. `gate-1` (development set): 56% before ADR 0011, 67% after | **Failing** (2 of the 10 answers needed) |
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

## 3b. Solved public cases (`gate-1b`): 30 cases, 2 of 2 confident answers correct

The fresh set for the accuracy line (ADR 0010, after ADR 0011 made `gate-1` a development set). Collected on
2026-11-27 with the fixed searches in `tests/public-cases/README.md`. The list q101–q138 was committed before any
search ran; every later batch (q139–q240) was committed before it was run. Labels were written and committed
(`gate-1b: 30 solved public cases with labels, written before scoring`) before `sherlog eval` was first run on the set.
No recipe, code or `gate-1` file was changed.

### How it was collected

| | |
|---|---|
| Searches written / run | 140 (q101–q240) / 134. Never run: q151, q155, q158, q188, q190, q210 |
| Threads opened or attempted | about 730; about 50 couldn't be read (blocked, 403/404, empty page, wrong page) |
| Cases that met the three rules | **30**, so about 4 threads in 100 (the first pass found 1 in 18) |
| Label strength | 5 `confirmed-by-fix`, 25 `owner-reported` |
| Sources | Tom's Hardware forums 16, Microsoft Q&A and Tech Community 9, Steam 2, Dell 1, Adobe 1, GOG 1 |

- Collecting stopped at 30, the minimum the gate asks for, with about 270 of the 1000 allowed reads unused.
- Threads were read by sub-agents that were told to read every page and answer NO unless the original poster, not a
  helper, said one change stopped the crash. Several long pages came back partly summarized by the fetch tool; the
  yes-cases rest on the posters' own posts as reported, so a few details (for example a stop code) may be slightly off.
- **The bar I applied** (the three ADR 0010 rules): the post shows a recorded fact; the poster reports the result as a
  fact (not "maybe", "for now", "so far today"); one change, not several at once; no Windows reinstall; no fix that
  later failed. Several dozen near-misses were left out on that bar, for example a poster who credited a CPU swap done
  together with a motherboard swap, or a fix that held one evening. A looser bar would have reached 30 sooner but
  every case on it is a judgment call, so the cases here lean to the clear ones.
- Medium-confidence cases kept: pc-1007 and pc-1009 (a power-supply swap happened around the same time), pc-1016
  (motherboard swapped after a power-supply swap failed), pc-1021, pc-1026 and pc-1027 (a processor replaced after other
  swaps), pc-1015 (XMP off, only 6 hours reported), pc-1018 and pc-1020 (posters' own answers, no duration).
- Where Windows records only a family of stop codes, the posts name them in words; they were turned into their usual
  hex codes (for example IRQL_NOT_LESS_OR_EQUAL as `0x0000000a`).

### Result (2026-11-27)

`sherlog eval tests/public-cases/gate-1b`:

| Measure | Result |
|---|---|
| Cases | 30 (gate needs 30) |
| Problems (groups of crashes) | 54 |
| Confirmed or Likely correct | **2 of 2 (100%)**, gate needs 80% |
| By label strength | confirmed-by-fix 0 of 0; owner-reported 2 of 2 |
| Wrong Confirmed | 0 |
| Unclear (not a gate line here) | 52 of 54 problems (96%) |

The two confident answers: **pc-1006** (the Cyberpunk launcher missing `MSVCP140_1.dll`, `runtime-vcredist-missing`)
and **pc-1022** (stop code 0x124, fixed by a new power supply, `bsod-whea-uncorrectable-error`). Both were correct.

### Misses

Confident and wrong: **none**. Every other problem was Unclear. The cases are listed by what fixed them, with what
Sherlog could and couldn't name:

| What fixed it | Cases | What Sherlog could name |
|---|---|---|
| A replaced part: memory (6) | pc-1004, 1007, 1008, 1009, 1013, 1029 | Only 0x1A, 0x50 and 0x124 stop codes point at memory, and those are Unclear. A graphics reset (pc-1004) came out as an NVIDIA driver reset, Unclear |
| A replaced part: processor (4) | pc-1001, 1021, 1026, 1027 | Nothing names a processor except 0x124 |
| A replaced part: motherboard (2), power supply (1), graphics card (1) | pc-1016, 1024, 1022 (confident, right), 1028 | Kernel-Power 41 alone says nothing about the part |
| A BIOS, memory or processor setting (6) | pc-1002, 1014, 1015, 1018, 1019, 1020 | No recipe covers a setting that isn't an overclock stop code |
| Software: a driver, overlay, antivirus, app build or service pack (9) | pc-1003, 1005, 1010, 1011, 1012, 1017, 1023, 1025, 1030 | Overlay and antivirus recipes are for named products (not GOG Galaxy or Avast on a 0x12B); app crashes in core Windows files stay Unclear |
| Missing Visual C++ file (1) | pc-1006 | Likely, correct |

For 6 of the 52 Unclear problems the matching recipe agreed with the label even though it said Unclear: pc-1007,
pc-1008 and pc-1009 (0x50, memory), pc-1025 (0xA, driver), and one group each in pc-1005 (0x139) and pc-1013 (0x1A).
Counting those would not change the gate line, which scores only Confirmed and Likely.

Labels, by recipe: `bsod-whea-uncorrectable-error` 9 (processor, motherboard or power-supply fixes, an overclock),
`bsod-memory-management` 5, `bsod-page-fault-in-nonpaged-area` 3, one each for `bsod-kernel-security-check-failure`,
`bsod-irql-not-less-or-equal`, `runtime-vcredist-missing` and `security-avast-avg-aswhook`, and 9 `none:` labels. Under
ADR 0011's convention, eight of the nine 0x124 labels sit on cases whose records have no 0x124 code, so Sherlog could
never say that recipe there; it doesn't affect the accuracy line, which only counts confident answers.

### What it shows

- **As first written, the accuracy line passed and told us almost nothing.** Two confident answers, both right. After
  ADR 0011 Sherlog says Unclear for 96% of these problems, so the line mostly rewarded staying quiet. On `gate-1` the
  same rules gave 4 of 6. The owner decided (ADR 0012) that the line needs at least 10 confident answers, so
  `gate-1b` now **fails** it (2 of 10).
- **Most fixes in solved threads are hardware or settings, not software.** 14 of 30 were a replaced part and 6 a
  BIOS or tuning setting. The records Windows keeps rarely tell those apart from a driver problem, which is why they
  come out Unclear.
- **Where Sherlog could be useful next** (each would need its own recipe rule and examples, and nothing is changed
  here): a Kernel-Power 41 with no stop code and a fix menu that includes the BIOS and power settings; blue screens
  with many different stop codes in one PC (pc-1005, 1013, 1014, 1021, 1024, 1026, 1027) pointing at hardware, which is
  the pattern the ADR 0011 note suggested.
- **`gate-1b` is frozen when this PR merges.** Cases aren't edited afterwards except to fix a copying mistake, and each
  such fix is listed here. If recipes change because of these misses, it becomes a development set and the gate needs
  `gate-1c`.

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
