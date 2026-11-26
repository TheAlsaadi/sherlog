# Recipe format (v0)

A recipe links something Windows records about a crash to a plain-language cause and to suggested fixes. Recipes live in `library/recipes/`, one YAML file per recipe, and must pass [`library/schema/recipe.schema.json`](../library/schema/recipe.schema.json). The decisions behind the format are in [ADR 0004](adr/0004-recipe-format-v0.md).

Recipes never contain code or commands. In Phase 1 Sherlog only *explains* crashes: fixes are shown as suggestions and nothing is run.

## A complete example

```yaml
id: gpu-nvidia-driver-reset
version: 1
title: NVIDIA graphics driver stopped responding
category: driver-reset
references:
  - https://learn.microsoft.com/en-us/windows-hardware/drivers/display/timeout-detection-and-recovery

# Only the display-reset event names the driver. A blue screen with stop code 0x116 can come from any
# graphics driver, so it belongs in a separate, vendor-neutral recipe.
match:
  any:
    - source: display-reset
      faulting_module: nvlddmkm

confidence:
  default: likely
  confirmed_when:
    - occurrences_within_days: { count: 3, days: 7 }

diagnosis:
  summary: "Your NVIDIA graphics driver stopped responding, and Windows had to restart it."
  details: "This usually comes from a recent driver update, an unstable overclock, or overheating."

fixes:
  - id: guide-clean-driver-install
    risk: advanced
    explain: "Reinstall the graphics driver cleanly from NVIDIA's website."
    actions:
      - action: show_guidance
        params: { guide: nvidia-clean-install }

examples:
  - source: display-reset
    faulting_module: nvlddmkm
```

## Fields

| Field | Required | What it is |
|---|---|---|
| `id` | yes | Lowercase words joined by `-`. Unique across the library. Labels and reports refer to it. |
| `version` | yes | Whole number, starting at 1. Raise it when you change what the recipe matches or says. |
| `title` | yes | Short name for maintainers. Users see `diagnosis.summary` instead. |
| `category` | yes | `crash`, `hang`, `driver-reset`, `bluescreen`, `shutdown` or `missing-file`. |
| `references` | yes | At least one `https://` link supporting the cause and the fixes: vendor docs, Microsoft Learn, or a widely cited bug report. |
| `priority` | no | 1–1000, default 100. Lower wins a tie (see "Which recipe wins"). Broad fallback recipes use a high number. |
| `match.any` | yes | One or more conditions. The recipe matches a crash when **any one** condition holds. |
| `confidence.default` | yes | `likely` or `unclear`. Never `confirmed`: confirmation has to be earned. |
| `confidence.confirmed_when` | no | Rules that raise the diagnosis to Confirmed (see below). |
| `diagnosis.summary` | yes | What happened, in one plain sentence a non-technical person understands. A short second sentence may say how sure we are or name the common causes ("Windows doesn't record which driver."). Under 200 characters. Use "app" for programs in general and "game" only in game-specific recipes; say "blue screen", "graphics driver", "processor" and "memory" rather than technical names. |
| `diagnosis.details` | no | Why this happens, in plain words. |
| `fixes` | no | Suggested fixes, best first. |
| `examples` | yes | At least one crash this recipe must explain (see "Examples"). |

### Conditions

A condition names an evidence `source` and at least one other field. **Every field in a condition must hold for the same crash.**

| Field | Compared with |
|---|---|
| `source` | `application-error` (Application Error 1000), `application-hang` (1002), `wer` (Windows Error Reporting 1001, live kernel events), `display-reset` (Display 4101), `bugcheck` (blue screen), `kernel-power` (unexpected shutdown, 41), `app-popup` (an app couldn't start because a DLL is missing: Application Popup 26), `reliability` (Reliability Monitor) |
| `app_name` | The app's file name, for example `Game.exe`. `*` matches any run of characters. |
| `faulting_module` | The file that failed, for example `nvwgf2umx.dll`; the driver name for display resets (`nvlddmkm`); the missing DLL for `app-popup` (`VCRUNTIME140.dll`). `*` allowed. |
| `exception_code` | `0x` plus 8 lowercase hex digits, for example `"0xc0000005"`. Quote it. |
| `bugcheck_code` | Same format, for example `"0x00000116"`. |
| `event_name` | The Windows Error Reporting event name, for example `LiveKernelEvent`. |
| `provider`, `event_id` | The event-log provider name and event ID, for rare cases the source alone doesn't pin down. |
| `message_contains` | Text that must appear in the event's message. Avoid it when a structured field exists: messages are translated on non-English Windows. |

Text comparisons ignore upper and lower case.

### Which recipe wins

When several recipes match the same crash group, Sherlog picks:

1. the one whose matching condition constrains the **most fields** (most specific),
2. then the lower `priority` number,
3. then the recipe `id` in alphabetical order.

So a recipe for "`Game.exe` crashing in `FakeOverlay64.dll`" beats a general "crash in an overlay DLL" recipe, and both beat a fallback recipe with `priority: 900`.

A field with a wildcard counts the same as an exact one, so `nv*.dll` and `nvwgf2umx.dll` are equally specific. Don't add fields only to win a tie; use `priority`, and let the `examples` test show you any overlap.

### Confidence

- A matched recipe gives its `confidence.default` (Likely or Unclear).
- A **Likely** recipe becomes **Confirmed** when any `confirmed_when` rule holds. `occurrences_within_days: {count: 3, days: 7}` means at least 3 crashes in the group within the 7 days ending at the most recent one (`count` 3–100, `days` 1–365). The window ends at the latest crash, not today, so a bundle diagnosed later gets the same answer.
- An **Unclear** recipe stays Unclear however often the crash repeats: repeats make a problem more certain, not its cause.
- A crash group no recipe matches is **Unclear**. Sherlog still says what it knows: the app, the file that failed, the error code and how often it happened.

### Fixes

| Field | What it is |
|---|---|
| `id` | Lowercase words joined by `-`. |
| `risk` | `safe`, `moderate` or `advanced`. |
| `explain` | What the fix does and why, in plain words. |
| `actions` | Steps from the approved-actions list only: `show_guidance`, `open_settings_page`, `open_url`, `verify_game_files`, `end_process`, `disable_startup_entry`, `rollback_driver`, `set_allowlisted_setting`, `run_system_check`. Each may have `params`. |
| `when` | Conditions that need the change timeline (Phase 2). Recorded now, not checked yet. |
| `verify.no_recurrence_days` | How many crash-free days count as "fixed". |

### Examples

Each entry under `examples` is the evidence of one crash this recipe must explain: a `source` plus the fields Windows would record (no wildcards). A test diagnoses every example against the **whole** library and fails if it lands on a different recipe or none. If your new recipe overlaps an existing one, that test tells you in your pull request.

## Checking a recipe

```powershell
dotnet test
```

CI also validates every file in `library/recipes/` against the schema.
