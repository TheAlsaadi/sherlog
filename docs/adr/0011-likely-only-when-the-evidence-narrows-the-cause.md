# ADR 0011: A recipe says Likely only when the evidence narrows the cause

- **Status:** Accepted
- **Date:** 2026-11-27
- **Extends:** ADR 0004 (recipe format and confidence)

## Context

The first set of solved public cases (`tests/public-cases/gate-1`, ADR 0010) scored 5 of 9 confident answers
correct. Two of the four misses came from graphics timeout recipes at Likely:

- **pc-0001:** "Your NVIDIA graphics driver stopped responding" for display resets that a faulty RAM stick caused.
- **pc-0012:** a 0x116 blue screen that underclocking the graphics card fixed.

The records behind these recipes (Display 4101 resets, LiveKernelEvent 141/117 and stop code 0x116) say only that
the graphics card didn't answer within about two seconds. Microsoft's and AMD's own lists of causes include the
driver, an overclock, memory settings, heat, the power supply and faulty parts. The recipes' own comments already said
the record doesn't name the cause, yet they said Likely, three of them could reach Confirmed after 3 resets in 7 days,
and some offered only driver fixes.

## Decision

**A recipe may say Likely only when the recorded evidence narrows the cause to what its summary names.** A record
that shows *what happened* but leaves the cause open among several unrelated things (driver or hardware) is Unclear,
however often it repeats.

- **Changed to Unclear, with `confirmed_when` removed:** `gpu-nvidia-driver-reset`, `gpu-amd-driver-reset`,
  `gpu-intel-driver-reset`, `gpu-live-kernel-video-timeout` and `gpu-video-tdr-failure`.
  - Their summaries now say what happened and add "Windows doesn't record why."
  - Their fixes now cover resetting overclocks and XMP, checking temperatures and testing memory, not only the driver
    (pinned by `LibraryExamplesTests`).
- **Kept at Likely:**
  - `bsod-whea-uncorrectable-error` (0x124): the stop code comes from the hardware's own error reporting, so it does
    narrow the cause to hardware, and the summary names those causes (overclock, heat, a faulty part).
  - The missing-runtime recipes: the record names the missing file, which is the cause.
- **Review for the rest of the library:** a new Likely recipe has to say, in its comment, what in the record narrows
  the cause. This is now in `docs/recipe-format.md`.

## Labeling convention for the next case set

pc-0013 and pc-0016 were labeled with the specific part (GPU clocks, power supply), and the 0x124 recipe was scored as
wrong, although its summary names exactly that kind of cause. From the next set on, `true_cause` is the recipe whose
**summary** names the cause the fix addressed, even if broadly ("a faulty part"). `none:<name>` is used only when no
recipe's summary covers it. `gate-1` keeps its labels as written, because changing them after scoring would bias the
result.

## Consequences

- **Fewer confident answers.** On `gate-1`, which is now a development set, the score moved from 5 of 9 to 4 of 6
  (67%). Both wrong graphics answers became Unclear. So did one correct AMD driver case (pc-0002), because the record
  can't tell it apart. The two remaining misses are the 0x124 labeling question above.
- **More Unclear on real PCs.** The Unclear share on real PCs (ADR 0009) can only go up from this change. That's the
  honest cost: Sherlog can't yet tell a driver reset from a hardware reset.
- **The way back to Likely is more evidence, not stronger wording.** For example, Phase 2B's change timeline
  ("resets began 2 days after the driver update"), or resets together with blue screens of many different codes,
  which points to hardware. Each would need its own recipe rule and examples.
- `gate-1` is now a development set (ADR 0010). Gate 1 needs a fresh set of cases for its accuracy line.
