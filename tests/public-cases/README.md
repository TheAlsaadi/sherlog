# Solved public cases

Real crashes from solved tech-support threads, used by Gate 1 to measure accuracy (ADR 0010). Each `*.yaml` file is
one thread. `sherlog eval <folder>` scores every case in a folder:

```powershell
dotnet run --project src/Sherlog.Cli -- eval tests/public-cases/gate-1
```

## Sets

| Folder | Status | Use |
|---|---|---|
| `gate-1/` | Frozen when merged | Scored for Gate 1. Don't tune recipes to its misses; if you do, it becomes a development set and the next run needs a fresh set |

## Case format

```yaml
format: 1
id: pc-0001                           # lowercase letters, digits, dashes; same as the file name
source_url: https://…                 # the thread (https only)
retrieved: 2026-11-27                 # when it was read
query: q01                            # which search in the list below found it
windows: Windows 11 23H2              # optional, as the post says
crashes:                              # what Windows recorded, as the post shows it
  - source: application-error         # application-error, application-hang, wer, display-reset,
                                      # bugcheck, kernel-power or app-popup
    app_name: game.exe                # optional
    faulting_module: nvwgf2umx.dll    # optional (file name only)
    exception_code: "0xc0000005"      # optional
    bugcheck_code: "0x00000116"       # optional (blue screens, live-kernel reports)
    event_name: LiveKernelEvent       # optional (Windows Error Reporting reports)
    count: 3                          # how many times, if the post says (default 1)
    days: 5                           # over how many days, if the post says (default 1)
truth:
  true_cause: gpu-nvidia-driver-reset # a recipe id, or none:<short-name> if no recipe describes the cause
  label_strength: owner-reported      # confirmed-by-fix (stayed fixed 7+ days) or owner-reported
  evidence: "Poster rolled back the driver; no crashes since." # one line, in our own words
```

Write only facts from the post, in our own words. No user names, no quoted text, no screenshots.

## Rules for collecting (ADR 0010)

A thread is used only when:

1. the post gives at least one recorded fact Sherlog reads (faulting module, exception code, stop code or live-kernel
   event name);
2. the person who had the crash says which fix stopped it;
3. the fix is specific enough to name a cause.

For each search below, go through the results in order and keep **every** thread that meets the rules, up to 3 per
search. Never skip a thread because of what Sherlog would say about it. Write the label from the thread **before**
running Sherlog on it.

## Fixed searches for `gate-1`

Written before collecting started (2026-11-27). Each is run as a web search; results come from any tech-support site.

| Id | Search | Covers |
|---|---|---|
| q01 | `"Faulting module name" nvwgf2umx.dll crash solved` | App crash in the NVIDIA user-mode driver |
| q02 | `"Faulting module name" amdxx64.dll OR atidxx64.dll crash fixed` | App crash in the AMD user-mode driver |
| q03 | `"Faulting module name: ucrtbase.dll" 0xc0000409 solved` | Fast-fail crashes |
| q04 | `"Faulting module name: KERNELBASE.dll" 0xe0434352 solved` | .NET app crashes |
| q05 | `"Faulting module name" GameOverlayRenderer64.dll OR DiscordHook64.dll OR RTSSHooks64.dll solved` | Overlay crashes |
| q06 | `"nvlddmkm stopped responding and has successfully recovered" solved` | NVIDIA driver resets |
| q07 | `"amdkmdag stopped responding" OR "amdwddmg stopped responding" solved` | AMD driver resets |
| q08 | `"rebooted from a bugcheck" 0x00000116 solved` | Graphics timeout blue screens |
| q09 | `"rebooted from a bugcheck" 0x0000001a solved` | Memory-management blue screens |
| q10 | `"rebooted from a bugcheck" 0x00000124 solved` | Hardware-error (WHEA) blue screens |
| q11 | `"Kernel-Power" "event 41" "BugcheckCode 0" solved` | Unexpected shutdowns |
| q12 | `"VCRUNTIME140.dll was not found" OR "MSVCP140.dll was not found" fixed` | Missing runtime |
| q13 | `"LiveKernelEvent" 141 solved` | Live-kernel graphics reports |
| q14 | `"Application Hang" "event 1002" solved` | App hangs |
