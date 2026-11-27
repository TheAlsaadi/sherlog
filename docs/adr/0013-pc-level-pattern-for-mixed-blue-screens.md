# ADR 0013: A recipe can match a pattern across a PC's blue screens

- **Status:** Accepted
- **Date:** 2026-11-27
- **Extends:** ADR 0004 (recipe format v0); keeps ADR 0011 (Likely only when the evidence narrows the cause)

## Context

Recipes see one crash group at a time, and blue screens are grouped by stop code. A PC whose blue screens show a
different stop code each time therefore gets one Unclear answer per code, although the mix is itself evidence.

On `gate-1b`, 8 of 30 PCs had blue screens with two or more different stop codes (pc-1005, 1013, 1014, 1021, 1024,
1026, 1027, 1029). Seven of them were fixed by hardware or a BIOS setting: new memory (pc-1013, pc-1029), a replaced
processor (pc-1021, 1026, 1027), a replaced motherboard (pc-1024) and turning off processor boost (pc-1014). One,
pc-1005, was fixed by going back to an older AMD graphics driver. `docs/concept.md` already promises "random blue
screens with different causes → possibly unstable memory or overclock", and ADR 0011 named this pattern as a way back
to Likely.

**Why the mix narrows the cause.** A faulty driver usually fails in the same way, so its blue screens repeat one stop
code and the record names the same driver. Memory that flips bits, a processor that is unstable at its boost clock or
voltage, a failing power supply or motherboard corrupt whatever code happens to be running, so each crash looks
different. The sources:

- Microsoft's stop-error troubleshooting guide ("Stop code error or bug check troubleshooting") says hardware problems
  "may cause erratic and unpredictable behavior", and its general steps are to update the BIOS and firmware and to
  run hardware and memory tests.
- Microsoft's bug-check page for users ("Bug Checks (Stop Code Errors)") lists memory errors (run Windows Memory
  Diagnostic), an outdated BIOS or firmware, and other hardware (run the maker's hardware diagnostics).
- Dell's advanced blue-screen guide calls faulty memory one of the most common hardware causes of stop errors, and
  has its own step for overheating.
- Microsoft Q&A volunteers say the same rule of thumb directly ("different BSOD codes usually mean hardware
  problems"). It is a volunteer answer, not Microsoft guidance, so it is supporting evidence only.

None of these sources gives a number. The thresholds below are our own, chosen to be strict, and they are checked
against development data only (see Consequences).

## Decision

### Format: an optional `pattern` block

A recipe has **either** `match` (one crash group, as today) **or** `pattern` (all the blue screens on a PC), never
both:

```yaml
pattern:
  distinct_stop_codes: { count: 3, days: 30 }
```

- **What counts as a blue screen:** a `bugcheck` record, a `kernel-power` record with a non-zero stop code (Windows
  wrote down a blue screen but the BugCheck record is missing), and a Reliability Monitor row for a blue screen.
- **What doesn't count:**
  - **Live-kernel reports** (`wer`, for example LiveKernelEvent 141 or 1a8): the PC didn't stop, so they aren't blue
    screens.
  - **Kernel-Power 41 with stop code 0**: the PC lost power without a blue screen. That is
    `power-unexpected-shutdown`'s question.
  - **The same stop code repeated**, however often: a driver that fails the same way every time is a single-code
    problem, so only *different* codes count. A blue screen and its Kernel-Power twin with the same code count once.
  - **Two different codes**: too easy to get from two unrelated problems, for example a graphics driver timeout and a
    separate network driver bug.
  - **Different codes spread over a long time**: only blue screens within `days` days, ending at the PC's most recent
    blue screen, count. The window ends at the latest blue screen, not today, so a bundle diagnosed later gets the
    same answer (as with `confirmed_when`).
- **The rule holds** when the blue screens in that window have at least `count` different non-zero stop codes.
  `count` is 3 to 20, `days` 1 to 90.
- **Then the recipe applies to every blue-screen group with a crash in the window, and it wins** over any single-code
  recipe, including 0x124 and 0x116: the pattern uses more of the record than one code does. Blue-screen groups
  outside the window, and every other kind of crash group, are diagnosed as before. If several pattern recipes hold,
  the lower `priority`, then the recipe id, wins.
- `Diagnoser.Diagnose(group)` sees one group, so a pattern can't hold there. Only `DiagnoseAll`, which `explain`,
  `crashes` and `eval` use, looks across groups.
- **What Sherlog says:** the recipe's summary, and "What we know" adds how many blue screens and different stop codes
  the PC had in the window.

### Confidence

- The first pattern recipe, `hardware-unstable-mixed-bluescreens`, is **Likely**: the record narrows the cause from
  "a driver or hardware" to "hardware or its settings" (memory, an overclock, boost or XMP, the power supply, the
  motherboard or its BIOS), which is what its summary names. It does not say which part.
- **A pattern recipe can't have `confirmed_when`** (the schema refuses it). Repeats are already the evidence the
  pattern uses, and a driver can still cause mixed codes (pc-1005), so the pattern never reaches Confirmed.
- Every other recipe keeps its confidence. ADR 0011 is unchanged.

### Examples for a pattern recipe

A pattern recipe's `examples` together describe **one PC**: each entry is one blue screen, all at the same time. The
library test diagnoses them together with `DiagnoseAll` and checks that every resulting group lands on the pattern
recipe. Every other recipe's examples are still diagnosed one at a time, so a pattern can never take them over, and
the pattern recipe needs at least `count` examples with different stop codes to pass its own test.

## Consequences

- Sherlog can give a confident answer for a PC with mixed blue screens instead of one Unclear answer per code.
- **This rule was designed from `gate-1b`'s misses**, so `gate-1b` is now a development set (ADR 0010). On it, the
  threshold of 3 codes takes in pc-1005, 1013, 1014, 1021, 1024, 1026 and 1027, of which 6 were hardware or a BIOS
  setting and 1 (pc-1005) a graphics driver. pc-1029 (2 codes, memory) stays out. That is development data, not a
  gate result: Gate 1 needs a fresh set (`gate-1c`).
- **It can be wrong.** A driver that corrupts memory, an antivirus or anti-cheat driver, or a broken Windows install
  can also cause mixed codes. The summary names causes, not one part, the details say a driver is still
  possible, and every fix is a check or a setting that can be put back.
- **Labeling.** From `gate-1c` on, a case with mixed blue screens fixed by hardware or a BIOS setting is labeled with
  this recipe, because its match fits the crash and its summary names the cause (`tests/public-cases/README.md`).
- The format grows by one optional block. Community recipes that don't use it are unaffected.
