# Sandbox tests

Sherlog never crashes programs, changes settings or applies fixes on a real PC. Tests that need that run in
**Windows Sandbox**, a throwaway copy of Windows that is wiped when it closes.

## Seeded crashes (Phase 1, Gate 1)

Small C programs in [`tests/seeded`](../tests/seeded) crash in known ways. Gate 1 needs Sherlog to read and match
**every** one of them. The first run (2026-11-25) also showed that a missing Visual C++ runtime is not logged as a
crash at all, but as an "Application Popup" (System log, event 26) naming the missing DLL; Sherlog now reads those.

| Program | What it does | Expected record |
|---|---|---|
| `SherlogSeedCrash.exe` | Crashes inside `SherlogSeedModule.dll` | Application Error 1000, module `SherlogSeedModule.dll`, `0xc0000005` |
| `SherlogSeedOverflow.exe` | Runs out of stack | Application Error 1000, `0xc00000fd` |
| `SherlogSeedFastFail.exe` | Ends itself with `__fastfail` | Application Error 1000, `0xc0000409` |
| `SherlogSeedNeedsRuntime.exe` | Needs the Visual C++ runtime, which the sandbox lacks | Application Popup 26 naming `VCRUNTIME140.dll`, matched by the library recipe `runtime-vcredist-missing` |

### Why not on GitHub Actions?

The plan (ADR 0007) was to crash these on CI runners. On 2026-11-25 we found that GitHub's `windows-latest` image
**never logs crash events**: its Application log had zero "Application Error" (1000) and zero Windows Error Reporting
(1001) events since the image was built, and none appeared after our programs crashed with the expected codes. The image
sets `ForceQueue` and `DontShowUI`, configures `LocalDumps`, and sets an `AeDebug` debugger. We don't change runner
settings to work around this, so CI only **builds** the programs (artifact `seeded-programs`) and the crashes run here.

### Run it

1. Download the programs from the latest CI run of your branch:

   ```powershell
   gh run download <run-id> --name seeded-programs --dir sandbox\.work\programs
   ```

2. Run:

   ```powershell
   .\sandbox\Run-Seeded.ps1
   ```

   A Windows Sandbox window opens, runs the crashes, collects a bundle and closes itself (a few minutes). The script
   then scores the bundle and prints whether the Gate 1 line passes. Output is in `sandbox\.work\out` (not committed).

## Not covered yet

- **A hung window (Application Hang 1002)** needs a GUI program that stops responding while someone tries to use it.
  Planned as a follow-up seeded program.
