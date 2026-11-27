# Solved public cases

Real crashes from solved tech-support threads, used by Gate 1 to measure accuracy (ADR 0010). Each `*.yaml` file is
one thread. `sherlog eval <folder>` scores every case in a folder:

```powershell
dotnet run --project src/Sherlog.Cli -- eval tests/public-cases/gate-1
```

## Sets

| Folder | Status | Use |
|---|---|---|
| `gate-1/` | **Development set** since 2026-11-27: scored at 56%, then recipes were changed from its misses (ADR 0011) | For checking recipe changes only. Gate 1's accuracy line needs a fresh set |

## Labeling convention (from the next set on, ADR 0011)

`true_cause` is the recipe whose **summary** names the cause the fix addressed, even if broadly. For example, the
0x124 recipe's "an overclock, overheating, or a faulty part" covers a fix that replaced the power supply. Use
`none:<short-name>` only when no recipe's summary covers the cause. `gate-1` keeps its original labels.

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

Each of q01–q14 was run twice on 2026-11-27, in standard and then extended mode, and the results of both were
checked in order. About 230 threads were read; 14 met the rules.

### Added 2026-11-27: solved-tag searches (ADR 0010 amendment)

Added because q01–q14 yielded too few cases. Written before any of them was run. Same rules and same order-taking,
up to 3 kept per search. A site's "solved" tag only finds the thread; the poster must still confirm the fix in the
thread itself (rule 2).

| Id | Search | Covers |
|---|---|---|
| q15 | `site:forums.tomshardware.com "[SOLVED]" "faulting module name"` | App crashes |
| q16 | `site:forums.tomshardware.com "[SOLVED]" "rebooted from a bugcheck"` | Blue screens |
| q17 | `site:forums.tomshardware.com "[SOLVED]" nvlddmkm "stopped responding"` | Graphics driver resets |
| q18 | `site:tenforums.com "Solved" BSOD bugcheck` | Blue screens |
| q19 | `site:elevenforum.com "Solved" BSOD` | Blue screens |
| q20 | `site:reddit.com/r/techsupport solved "faulting module name"` | App crashes |
| q21 | `site:reddit.com/r/techsupport solved LiveKernelEvent` | Live-kernel reports |
| q22 | `site:tenforums.com "Solved" "faulting module"` | App crashes |

## Fixed searches for `gate-1b`

`gate-1b` is the fresh set for Gate 1's accuracy line (ADR 0010: `gate-1` became a development set under ADR 0011).
Labels follow the convention above. The list below was written on 2026-11-27 **before any search was run**, and it
reuses none of the exact query texts of q01–q22. Same rules as for `gate-1`: each search is run in standard mode (and
extended mode when standard gives few threads), results are taken in order, every thread that meets the three rules
is kept up to 3 per search, and URLs already in `gate-1/` are excluded. If these run dry before 30 cases, more
searches are added to this section, written down before they are run.

| Id | Search | Covers |
|---|---|---|
| q101 | `"Faulting module name: nvwgf2umx.dll" game crashing fixed by` | App crash in the NVIDIA user-mode driver |
| q102 | `"Faulting module name: atidxx64.dll" OR "amdxx64.dll" game crashes fixed` | App crash in the AMD user-mode driver |
| q103 | `"Faulting module name: igd10iumd64.dll" OR "igdumdim64.dll" crash fixed` | App crash in the Intel user-mode driver |
| q104 | `"Faulting module name: ntdll.dll" "0xc0000374" heap corruption fixed` | Heap-corruption crashes |
| q105 | `"Faulting module name: KERNELBASE.dll" "0xc0000005" OR "0x80000003" fixed` | Crashes inside a Windows system file |
| q106 | `"Faulting module name: ucrtbase.dll" "0xc0000409" game crash fixed by` | Fast-fail crashes in games |
| q107 | `"Faulting module name: clr.dll" OR "coreclr.dll" crash fixed .NET` | .NET runtime crashes |
| q108 | `"Faulting module name: d3d11.dll" OR "dxgi.dll" OR "d3d12.dll" crash fixed` | Direct3D crashes |
| q109 | `"Faulting module name" "MSVCP140.dll" OR "VCRUNTIME140.dll" crash solved` | Crashes inside the C++ runtime |
| q110 | `"Faulting module name" "0xc000001d" illegal instruction game crash solved` | Illegal-instruction crashes |
| q111 | `"Faulting module name" BEClient OR EasyAntiCheat OR vgk crash solved` | Anti-cheat crashes |
| q112 | `"Faulting module name" overlay OR hook dll crash disabled fixed game` | Overlay and injected-DLL crashes |
| q113 | `"Faulting module name" avghookx OR aswhook OR atcuf64 OR sysfer crash fixed` | Security-software crashes |
| q114 | `"Faulting application name" "Application Hang" "stopped interacting with Windows" fixed` | App hangs |
| q115 | `"Display driver nvlddmkm stopped responding and has successfully recovered" fixed by` | NVIDIA driver resets |
| q116 | `"Display driver amdkmdap stopped responding" OR "amdwddmg stopped responding" fixed by` | AMD driver resets |
| q117 | `"Display driver igfx stopped responding" OR "igfxn stopped responding" fixed` | Intel driver resets |
| q118 | `"VIDEO_TDR_FAILURE" bugcheck "0x00000116" fixed by` | Graphics timeout blue screens |
| q119 | `"LiveKernelEvent" "WATCHDOG" OR "141" OR "117" "Hardware Error" fixed by` | Live-kernel graphics reports |
| q120 | `"WHEA_UNCORRECTABLE_ERROR" "0x124" fixed by` | Hardware-error blue screens |
| q121 | `"MEMORY_MANAGEMENT" OR "PAGE_FAULT_IN_NONPAGED_AREA" "0x0000001a" OR "0x00000050" fixed by` | Memory blue screens |
| q122 | `"DRIVER_IRQL_NOT_LESS_OR_EQUAL" OR "IRQL_NOT_LESS_OR_EQUAL" "0x000000d1" OR "0x0000000a" fixed` | Driver blue screens |
| q123 | `"SYSTEM_SERVICE_EXCEPTION" OR "KERNEL_SECURITY_CHECK_FAILURE" "0x0000003b" OR "0x00000139" fixed` | Blue screens in system services |
| q124 | `"CRITICAL_PROCESS_DIED" OR "DPC_WATCHDOG_VIOLATION" OR "CLOCK_WATCHDOG_TIMEOUT" fixed by` | Watchdog and critical-process blue screens |
| q125 | `"INACCESSIBLE_BOOT_DEVICE" OR "KERNEL_DATA_INPAGE_ERROR" OR "NTFS_FILE_SYSTEM" bugcheck fixed` | Disk-related blue screens |
| q126 | `"Kernel-Power" "41" "BugcheckCode" "0" random restart fixed by` | Unexpected shutdowns |
| q127 | `"The system has rebooted without cleanly shutting down" Kernel-Power 41 power supply OR RAM fixed` | Unexpected shutdowns |
| q128 | `"was not found" "VCRUNTIME140_1.dll" OR "MSVCP140.dll" OR "api-ms-win-crt-runtime" fixed by installing` | Missing C++ runtime |
| q129 | `"xinput1_3.dll" OR "d3dx9_43.dll" OR "MSVCR100.dll" "was not found" fixed by installing` | Missing DirectX or old runtime |
| q130 | `"Application Popup" OR "System Error" "was not found" dll game won't start fixed` | Missing-file startup failures |
| q131 | `".NET" "Application Error" "0xe0434352" "Exception info" fixed by` | .NET unhandled exceptions |
| q132 | `site:answers.microsoft.com OR site:learn.microsoft.com/en-us/answers "bugcheck" "I fixed it by"` | Blue screens, Microsoft Q&A |
| q133 | `site:forums.tomshardware.com "faulting module name" "fixed it" OR "problem solved"` | App crashes, Tom's Hardware |
| q134 | `site:steamcommunity.com "Faulting module name" "fixed" crash discussion` | Game crashes, Steam |
| q135 | `site:reddit.com "Faulting module name" "fixed it" OR "that fixed" crash` | App crashes, Reddit |
| q136 | `site:reddit.com "rebooted from a bugcheck" "fixed" OR "solved"` | Blue screens, Reddit |
| q137 | `site:github.com issues "Faulting module name" windows crash fixed by` | App crashes, GitHub issues |
| q138 | `site:community.adobe.com OR site:dell.com/community "Faulting module name" fixed` | App crashes, vendor communities |

### Added 2026-11-27 for `gate-1b`: more searches (q139–q160)

Added because q101–q138 (standard mode) gave too few cases from about 300 threads read: most Microsoft Q&A threads end
without a word from the poster. Written before any of these were run. Same rules and same order-taking. Several aim at
sites where posters more often report back (Tom's Hardware solved threads, Steam discussions, vendor communities).

| Id | Search | Covers |
|---|---|---|
| q139 | `forums.tomshardware.com [SOLVED] BSOD PAGE_FAULT_IN_NONPAGED_AREA replaced RAM` | Memory blue screens |
| q140 | `forums.tomshardware.com [SOLVED] WHEA_UNCORRECTABLE_ERROR 0x124 fixed` | Hardware-error blue screens |
| q141 | `forums.tomshardware.com [SOLVED] "stopped responding and has successfully recovered" fixed` | Graphics driver resets |
| q142 | `forums.tomshardware.com [SOLVED] Kernel-Power 41 random restart fixed` | Unexpected shutdowns |
| q143 | `forums.tomshardware.com [SOLVED] game crashes "faulting module" fixed` | App crashes |
| q144 | `forums.tomshardware.com [SOLVED] KERNEL_DATA_INPAGE_ERROR OR CRITICAL_PROCESS_DIED BSOD` | Disk and critical-process blue screens |
| q145 | `forums.tomshardware.com [SOLVED] DRIVER_IRQL_NOT_LESS_OR_EQUAL OR SYSTEM_SERVICE_EXCEPTION BSOD` | Driver blue screens |
| q146 | `forums.tomshardware.com [SOLVED] VIDEO_TDR_FAILURE nvlddmkm OR atikmpag` | Graphics timeout blue screens |
| q147 | `forums.tomshardware.com [SOLVED] MEMORY_MANAGEMENT 0x1a BSOD` | Memory blue screens |
| q148 | `steamcommunity.com discussions "Faulting module name" "I fixed it"` | Game crashes, Steam |
| q149 | `steamcommunity.com discussions crash "Event Viewer" "exception code" "solved by"` | Game crashes, Steam |
| q150 | `techcommunity.microsoft.com "faulting module" crash resolved` | App crashes |
| q151 | `community.adobe.com "faulting module" crash "solved" OR "fixed" Windows` | App crashes, Adobe |
| q152 | `"Application Error" "faulting module" "after updating" crash fixed rolled back` | App crashes after updates |
| q153 | `"Faulting module name" crash "disabled" "no more crashes" overlay OR antivirus` | Overlay and security-software crashes |
| q154 | `"stopped responding and has successfully recovered" "after I replaced" OR "turned out to be"` | Graphics driver resets |
| q155 | `"LiveKernelEvent" 141 OR 117 "problem went away" OR "turned out to be"` | Live-kernel graphics reports |
| q156 | `"Kernel-Power" 41 "after replacing" OR "turned out to be" power supply stopped restarting` | Unexpected shutdowns |
| q157 | `"bugcheck" "0x00000124" OR "0x00000116" "turned out to be" OR "problem went away"` | Hardware-error and graphics blue screens |
| q158 | `"was not found" "VCRUNTIME140.dll" OR "MSVCP140.dll" game "that worked" OR "problem solved"` | Missing runtime |
| q159 | `"Application Hang" "Event 1002" "turned out to be" OR "solved" fixed` | App hangs |
| q160 | `"Faulting module name" "0xc0000005" "turned out to be" OR "culprit was" crash` | App crashes |

### Added 2026-11-27 for `gate-1b`: more Tom's Hardware solved-thread searches (q161–q175)

q139–q147 found that Tom's Hardware threads, where posters tend to report back, qualified more often than other sites.
These variations were written before any of them was run. Same rules.

| Id | Search | Covers |
|---|---|---|
| q161 | `forums.tomshardware.com [SOLVED] "stopped responding" nvlddmkm replaced OR rolled back` | Graphics driver resets |
| q162 | `forums.tomshardware.com [SOLVED] random shutdown power supply replaced fixed` | Unexpected shutdowns |
| q163 | `forums.tomshardware.com [SOLVED] BSOD "new RAM" OR "bad RAM" fixed` | Memory blue screens |
| q164 | `forums.tomshardware.com [SOLVED] BSOD graphics driver rollback fixed` | Driver blue screens |
| q165 | `forums.tomshardware.com [SOLVED] BSOD XMP OR overclock disabled fixed` | Overclock-related blue screens |
| q166 | `forums.tomshardware.com [SOLVED] game crashes to desktop "Application Error" event viewer` | App crashes |
| q167 | `forums.tomshardware.com [SOLVED] LiveKernelEvent OR "event 141"` | Live-kernel reports |
| q168 | `forums.tomshardware.com [SOLVED] 0xc0000005 crash overclock OR undervolt OR XMP` | App crashes |
| q169 | `forums.tomshardware.com [SOLVED] amdkmdag OR atikmpag black screen driver timeout` | AMD driver resets |
| q170 | `forums.tomshardware.com [SOLVED] WHEA Logger 0x124 CPU voltage OR BIOS` | Hardware-error blue screens |
| q171 | `forums.tomshardware.com [SOLVED] CLOCK_WATCHDOG_TIMEOUT OR DPC_WATCHDOG_VIOLATION` | Watchdog blue screens |
| q172 | `forums.tomshardware.com [SOLVED] "VCRUNTIME140" OR "MSVCP140" OR "xinput1_3" missing` | Missing runtime |
| q173 | `forums.tomshardware.com [SOLVED] application "not responding" freezing "event 1002" OR hang` | App hangs |
| q174 | `forums.tomshardware.com [SOLVED] SYSTEM_THREAD_EXCEPTION_NOT_HANDLED OR KMODE_EXCEPTION_NOT_HANDLED` | Driver blue screens |
| q175 | `forums.tomshardware.com [SOLVED] UNEXPECTED_STORE_EXCEPTION OR "INACCESSIBLE_BOOT_DEVICE" OR NTFS_FILE_SYSTEM` | Disk-related blue screens |

### Added 2026-11-27 for `gate-1b`: posts where the poster writes up their own fix (q176–q190)

q101–q175 gave about 18 cases from over 400 threads, and the best ones were threads where the poster came back to say
what fixed it. These searches aim at that kind of post. Written before any of them was run. Same rules.

| Id | Search | Covers |
|---|---|---|
| q176 | `"how I fixed" "Faulting module name" crash game Windows` | App crashes |
| q177 | `"how I fixed" BSOD bugcheck "my fix"` | Blue screens |
| q178 | `"[Fixed]" CTD "exception code" forum` | Game crashes |
| q179 | `"what fixed it for me" "Faulting module name"` | App crashes |
| q180 | `"my solution" "nvlddmkm" OR "amdkmdag" display driver stopped responding` | Graphics driver resets |
| q181 | `"for anyone else" "faulting module" "the fix was"` | App crashes |
| q182 | `"for anyone with this problem" bugcheck "the fix was" OR "what worked"` | Blue screens |
| q183 | `"LiveKernelEvent" "for anyone" "what worked" OR "the fix was"` | Live-kernel reports |
| q184 | `"Kernel-Power" 41 "for anyone" "the fix was" OR "what worked"` | Unexpected shutdowns |
| q185 | `"was not found" "for anyone" "what worked" OR "the fix was" dll game launch` | Missing runtime |
| q186 | `forums.flightsimulator.com "[Fixed]" OR "solved" CTD "exception code" OR "faulting module"` | Game crashes |
| q187 | `forums.ageofempires.com OR forum.paradoxplaza.com crash "faulting module" "fixed it"` | Game crashes |
| q188 | `github.com issues Windows crash "0xc0000005" "faulting module" "was caused by" OR "fixed by updating"` | App crashes, GitHub issues |
| q189 | `community.adobe.com "faulting module" "solved" "I fixed" Photoshop OR Premiere OR Lightroom` | App crashes, Adobe |
| q190 | `forums.unrealengine.com OR forum.unity.com "faulting module" crash "fixed by" Windows editor` | App crashes, game engines |

### Added 2026-11-27 for `gate-1b`: more Tom's Hardware searches by outcome wording (q191–q200)

Written before any of them was run. Tom's Hardware threads gave the best yield so far, so these vary the wording that
solved threads tend to contain. Same rules.

| Id | Search | Covers |
|---|---|---|
| q191 | `forums.tomshardware.com [SOLVED] BSOD "replaced the" PSU OR RAM OR GPU "no more"` | Blue screens |
| q192 | `forums.tomshardware.com [SOLVED] "stopped crashing" after BIOS update OR "BIOS update" BSOD` | Blue screens |
| q193 | `forums.tomshardware.com [SOLVED] ntoskrnl.exe BSOD "was the RAM" OR "was the PSU" OR "was the GPU"` | Blue screens |
| q194 | `forums.tomshardware.com [SOLVED] random restarts "event 41" "it was the"` | Unexpected shutdowns |
| q195 | `forums.tomshardware.com [SOLVED] game crashing "0xc0000005" "it was the"` | App crashes |
| q196 | `forums.tomshardware.com [SOLVED] nvlddmkm OR LiveKernelEvent "it was the"` | Graphics driver resets |
| q197 | `forums.tomshardware.com [SOLVED] WHEA "it was the"` | Hardware-error blue screens |
| q198 | `forums.tomshardware.com [SOLVED] BSOD "Core Performance Boost" OR "Precision Boost" OR XMP disabled stable` | Overclock-related blue screens |
| q199 | `forums.tomshardware.com [SOLVED] GPU undervolt OR underclock black screen crash fixed` | Graphics driver resets |
| q200 | `forums.tomshardware.com [SOLVED] "Application Error" OR "Event ID 1000" crash fixed "it was"` | App crashes |

### Added 2026-11-27 for `gate-1b`: other forums (q201–q210)

q101–q200 gave 24 cases from about 550 threads, short of 30. These aim at other forums where posters report back.
Written before any of them was run. Same rules.

| Id | Search | Covers |
|---|---|---|
| q201 | `forums.tomsguide.com [SOLVED] BSOD bugcheck fixed replaced` | Blue screens |
| q202 | `forums.tomsguide.com [SOLVED] "stopped responding" display driver OR LiveKernelEvent fixed` | Graphics driver resets |
| q203 | `forums.tomsguide.com [SOLVED] random restart Kernel-Power 41 fixed` | Unexpected shutdowns |
| q204 | `bleepingcomputer.com forums BSOD bugcheck "the problem was" OR "fixed it"` | Blue screens |
| q205 | `forums.anandtech.com BSOD OR "Kernel-Power" fixed "it was the"` | Blue screens, shutdowns |
| q206 | `overclock.net BSOD WHEA OR 0x124 OR LiveKernelEvent "it was the" fixed` | Hardware-error blue screens |
| q207 | `techsupportforum.com OR geekstogo.com BSOD crash bugcheck "problem solved"` | Blue screens |
| q208 | `forums.whirlpool.net.au BSOD OR "Kernel-Power" crash fixed "it was"` | Blue screens, shutdowns |
| q209 | `forum.notebookreview.com OR community.acer.com OR forums.lenovo.com BSOD OR "stopped responding" fixed laptop` | Laptop crashes |
| q210 | `windowscentral.com forums OR howtogeek.com BSOD crash fixed bugcheck` | Blue screens |

### Added 2026-11-27 for `gate-1b`: Tom's Hardware solved threads by symptom (q211–q226)

q201–q210 (other forums) gave no new cases, so these go back to Tom's Hardware solved threads with more symptom
wording. Written before any of them was run. Same rules.

| Id | Search | Covers |
|---|---|---|
| q211 | `forums.tomshardware.com [SOLVED] game crash to desktop nvidia driver fixed` | App crashes |
| q212 | `forums.tomshardware.com [SOLVED] games crash AMD Radeon driver timeout fixed` | App crashes, graphics resets |
| q213 | `forums.tomshardware.com [SOLVED] PC freezes random event viewer fixed` | Hangs |
| q214 | `forums.tomshardware.com [SOLVED] BSOD 0x0000009f OR 0x0000001e OR 0x0000007e fixed` | Blue screens |
| q215 | `forums.tomshardware.com [SOLVED] BSOD SSD OR HDD replaced fixed kernel data inpage` | Disk-related blue screens |
| q216 | `forums.tomshardware.com [SOLVED] BSOD new build motherboard replaced fixed` | Blue screens |
| q217 | `forums.tomshardware.com [SOLVED] BSOD after Windows update rolled back fixed` | Blue screens |
| q218 | `forums.tomshardware.com [SOLVED] BSOD driver uninstalled fixed dump` | Driver blue screens |
| q219 | `forums.tomshardware.com [SOLVED] application not responding hang fixed program` | App hangs |
| q220 | `forums.tomshardware.com [SOLVED] BSOD overheating CPU cooler fixed` | Blue screens |
| q221 | `forums.tomshardware.com [SOLVED] BSOD network OR wifi OR audio OR USB driver fixed` | Driver blue screens |
| q222 | `forums.tomshardware.com [SOLVED] laptop random shutdown battery OR charger fixed Kernel-Power` | Unexpected shutdowns |
| q223 | `forums.tomshardware.com [SOLVED] PC crash during gaming PSU replaced fixed` | Unexpected shutdowns, crashes |
| q224 | `forums.tomshardware.com [SOLVED] BSOD Curve Optimizer OR undervolt Ryzen WHEA fixed` | Hardware-error blue screens |
| q225 | `forums.tomshardware.com [SOLVED] MEMORY_MANAGEMENT OR PAGE_FAULT_IN_NONPAGED_AREA RAM replaced fixed` | Memory blue screens |
| q226 | `forums.tomshardware.com [SOLVED] IRQL_NOT_LESS_OR_EQUAL RAM OR driver fixed` | Driver blue screens |
