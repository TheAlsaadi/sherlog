# Sherlog: Build Plan for Claude Code

> **Sherlog** is a free, open-source Windows app that answers three questions in plain language: *Why did it crash? Why is it slow? Is my PC safe and healthy?* Then it fixes the cause, and every fix can be undone.
>
> The full product concept is in [`docs/concept.md`](docs/concept.md). This file is the **execution plan**: Claude Code follows it phase by phase, from an empty folder to a public release.

---

## 0. How Claude Code uses this file

1. At the start of every session, read **this file**, `CLAUDE.md`, `docs/concept.md` and every ADR in `docs/adr/`.
2. Find the **first unchecked task** in the current phase. Work only on that phase.
3. Follow **the phase loop** (section 4) for every phase, with no shortcuts.
4. A phase is finished only when its **gate** passes and the evidence is written to `docs/gates/phase-N.md`.
5. Tick checkboxes in this file as tasks finish, and commit the change with the work.
6. When a task is marked **👤 OWNER**, stop and ask the owner. Do not guess, and do not work around it.

---

## 1. Ground rules (never break these)

**Safety on real PCs**
- **Never apply a fix, change a driver, edit the registry, or change system settings on the development PC.** Fix actions are tested only inside **Windows Sandbox** or a **Hyper-V virtual machine**.
- **Never disable or weaken** Microsoft Defender, the firewall, SmartScreen, UAC, Secure Boot or any security feature, including "temporarily for testing."
- **Ask the owner before running any command that needs admin rights** on the development PC.
- **Never inject code into, or hook, a game or other process.** Measure from outside only (event logs, WMI, Windows event tracing).

**Privacy**
- **Never commit real crash dumps, event logs, user names, machine names, file paths from a user profile, or any personal data.** Test fixtures must be synthetic or redacted with `sherlog collect --redact`.
- Crash dumps never leave the PC they were made on, not even to Claude Code's context. Use summaries only.

**Engineering**
- **Test-driven:** write the failing test first, every time.
- **The fix library holds recipes, not scripts.** The fix engine executes only actions on the approved-actions list (Appendix C). Never add a "run command" or "run script" action.
- **Every fix action has four parts:** `Describe` (dry run), `Apply`, `Verify`, `Undo`. The undo record is saved **before** `Apply` runs.
- **User-facing text** is plain language, has no jargon and no scare tactics, and states a confidence level.
- **Small commits, conventional messages** (`feat:`, `fix:`, `test:`, `docs:`, `chore:`), one pull request per task group.

**Pull requests and merging**
- Claude Code opens a pull request for every branch with `gh pr create`.
- Claude Code may merge its own pull request **after CI passes and the code-review skill has run**, **except** pull requests that touch `src/Sherlog.Service/`, the fix engine, IPC, the approved-actions list, signing, or release workflows. Those need **👤 OWNER** approval.
- Never force-push to `main`. Never bypass branch protection.

---

## 2. Before starting: owner decisions and prerequisites

### 👤 OWNER decisions (ask all of these in one message at the start of Phase 0)

| Decision | Recommended default |
|---|---|
| GitHub owner for the repo (personal account or an organization) | Personal account. Organizations must be created on github.com; `gh` cannot create them |
| Repository name and visibility | `sherlog`, public |
| Code license | MIT |
| License for the recipe library (`library/`) | CC BY 4.0 |
| May Claude Code merge its own non-security PRs after CI passes? | Yes (see the merge rule above) |
| Is Windows Sandbox or Hyper-V available on the development PC? | Needed from Phase 2; Windows 11 Pro or higher |

### Development machine prerequisites (check them; ask the owner to install anything missing)

- Windows 11 (Pro or higher for Windows Sandbox and Hyper-V)
- Git, and GitHub CLI authenticated (`gh auth status`)
- .NET SDK, current LTS (.NET 10): `dotnet --list-sdks`
- Windows App SDK and WinUI 3 tooling (needed from Phase 2D; Visual Studio 2022+ with the "WinUI application development" workload is the most reliable path)
- Optional from Phase 1: Debugging Tools for Windows (for blue-screen dump analysis)

---

## 3. Skills map

Use these skills at the moments listed. If a skill is not installed in this Claude Code setup, say which one is missing and follow its intent manually.

| When | Skill |
|---|---|
| Start of every phase: shape the design, settle open questions | `superpowers:brainstorming` |
| Turn the phase into a detailed, bite-sized task plan | `superpowers:writing-plans` |
| Record decisions (ADRs) and keep `docs/GLOSSARY.md` current | `anthropic-skills:domain-modeling` |
| Isolate each phase's work on its own branch | `superpowers:using-git-worktrees` |
| Carry out the plan, task by task | `superpowers:subagent-driven-development` (independent tasks) or `superpowers:executing-plans` |
| Independent parallel work (for example researching many crash signatures) | `superpowers:dispatching-parallel-agents` |
| Writing any code | `superpowers:test-driven-development` |
| Any failing test, bug or unexpected behavior | `superpowers:systematic-debugging` |
| End of each task group | `superpowers:requesting-code-review`, then `superpowers:receiving-code-review` |
| Before claiming anything is done or a gate is passed | `superpowers:verification-before-completion` |
| Merging a phase branch | `superpowers:finishing-a-development-branch` |
| App screens and visual direction (Phase 2D) | `frontend-design:frontend-design` (use its design principles; the UI is WinUI 3, not web) |
| All user-facing wording: diagnoses, buttons, notifications | `design:ux-copy` |
| Accessibility audit of the app (Phase 2D, 5) | `design:accessibility-review` |
| Reviewing screenshots of the app | `design:design-critique` |
| MCP server (Phase 4) | `anthropic-skills:mcp-builder` |
| README, release notes, docs aimed at users | `anthropic-skills:humanizer` (final pass for plain, natural text) |

---

## 4. The phase loop (the structured approach)

Run this loop for **every phase**:

1. **Load context.** Read this plan's phase section, `docs/concept.md`, `CLAUDE.md` and all ADRs.
2. **Brainstorm** (`superpowers:brainstorming`). Settle the phase's design. Ask the owner only about items marked 👤 OWNER or genuinely blocking.
3. **Decide and record.** Write an ADR for each significant decision in `docs/adr/NNNN-title.md` (`anthropic-skills:domain-modeling`).
4. **Plan** (`superpowers:writing-plans`). Write `docs/plans/phase-N-<name>.md` with small, testable tasks.
5. **Track.** Make sure each task has a GitHub issue in the phase milestone (`gh issue create --milestone "..."`).
6. **Branch** (`superpowers:using-git-worktrees`): `phase-N/<short-name>`.
7. **Build** with `superpowers:test-driven-development`, using `superpowers:subagent-driven-development` or `superpowers:executing-plans`. Debug with `superpowers:systematic-debugging`.
8. **Review** (`superpowers:requesting-code-review`), then fix the findings (`superpowers:receiving-code-review`).
9. **Verify the gate** (`superpowers:verification-before-completion`). Write the evidence (test output, measurements, accuracy numbers) to `docs/gates/phase-N.md`.
10. **Ship the phase** (`superpowers:finishing-a-development-branch`): `gh pr create`, CI green, merge per the merge rule, close the issues and the milestone.
11. **Update this file.** Tick the boxes and add anything learned to the next phase's notes.

---

## 5. Target repository layout

```
sherlog/
├─ plan.md                      ← this file
├─ CLAUDE.md                    ← rules for Claude Code (Appendix A)
├─ README.md                    ← user-facing intro (from docs/concept.md)
├─ LICENSE                      ← code license (MIT)
├─ CONTRIBUTING.md  SECURITY.md  CODE_OF_CONDUCT.md
├─ Directory.Build.props        ← shared .NET settings (nullable, warnings as errors)
├─ Sherlog.sln
├─ docs/
│  ├─ concept.md                ← the product concept
│  ├─ GLOSSARY.md
│  ├─ adr/                      ← architecture decision records
│  ├─ plans/                    ← detailed per-phase plans
│  ├─ gates/                    ← evidence that each gate passed
│  ├─ threat-model.md           ← from Phase 2A
│  └─ recipe-format.md
├─ library/
│  ├─ LICENSE                   ← CC BY 4.0
│  ├─ schema/recipe.schema.json
│  └─ recipes/*.yaml
├─ src/
│  ├─ Sherlog.Core/             ← evidence models, readers, recipe engine, diagnosis
│  ├─ Sherlog.Cli/              ← `sherlog` command-line tool (Phase 1)
│  ├─ Sherlog.Service/          ← recorder + fix executor, runs elevated (Phase 2)
│  ├─ Sherlog.App/              ← WinUI 3 app (Phase 2D)
│  └─ Sherlog.Mcp/              ← MCP server (Phase 4)
├─ tests/
│  ├─ Sherlog.Core.Tests/
│  ├─ Sherlog.Cli.Tests/
│  ├─ Sherlog.Service.Tests/
│  └─ fixtures/                 ← synthetic or redacted evidence only
├─ sandbox/                     ← Windows Sandbox (.wsb) configs and VM test scripts
└─ .github/
   ├─ workflows/ci.yml
   ├─ dependabot.yml
   ├─ CODEOWNERS
   ├─ PULL_REQUEST_TEMPLATE.md
   └─ ISSUE_TEMPLATE/ (bug.yml, recipe-request.yml, feature.yml)
```

---

## 6. Phase 0: Repository setup

**Goal:** a public, well-organized, protected repository with CI, ready for Phase 1.
**Skills:** `superpowers:brainstorming` (owner decisions), `anthropic-skills:domain-modeling` (first ADRs), `superpowers:verification-before-completion`.

### Tasks

- [x] **👤 OWNER:** ask the decision questions from section 2 in one message.
- [x] Check the tooling:
  ```bash
  gh auth status
  git --version
  dotnet --list-sdks
  OWNER=$(gh api user -q .login)   # or the organization the owner chose
  gh repo view "$OWNER/sherlog" >/dev/null 2>&1 && echo "Name already taken under $OWNER" || echo "Name available"
  ```
- [x] Create the local repo and skeleton **in the folder that already holds this `plan.md` and `docs/concept.md`** (the owner's starting folder; do not create a second folder):
  ```bash
  git init -b main
  dotnet new gitignore
  dotnet new editorconfig
  dotnet new sln -n Sherlog
  dotnet new classlib -n Sherlog.Core -o src/Sherlog.Core
  dotnet new console  -n Sherlog.Cli  -o src/Sherlog.Cli
  dotnet new xunit    -n Sherlog.Core.Tests -o tests/Sherlog.Core.Tests
  dotnet new xunit    -n Sherlog.Cli.Tests  -o tests/Sherlog.Cli.Tests
  dotnet sln add src/Sherlog.Core src/Sherlog.Cli tests/Sherlog.Core.Tests tests/Sherlog.Cli.Tests
  dotnet add src/Sherlog.Cli reference src/Sherlog.Core
  dotnet add tests/Sherlog.Core.Tests reference src/Sherlog.Core
  dotnet add tests/Sherlog.Cli.Tests reference src/Sherlog.Cli
  dotnet build && dotnet test
  ```
  (Service, App and MCP projects are added in their own phases.)
- [x] Add `Directory.Build.props`: target the current LTS framework (Windows-specific projects use the `-windows` target framework), `<Nullable>enable</Nullable>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, `<ImplicitUsings>enable</ImplicitUsings>`.
- [x] Add documents:
  - `docs/concept.md` (already in place; provided with this plan)
  - `CLAUDE.md` (Appendix A)
  - `README.md`: a short, plain-language intro written from the concept, with a "Status: early development" note (`anthropic-skills:humanizer` final pass)
  - `LICENSE` (MIT, with the owner's name and the current year) and `library/LICENSE` (CC BY 4.0)
  - `CONTRIBUTING.md` (how to build, test, and submit a recipe), `SECURITY.md` (private reporting through GitHub security advisories), `CODE_OF_CONDUCT.md` (Contributor Covenant)
  - `docs/GLOSSARY.md` (evidence, diagnosis, confidence, recipe, action, undo record, change timeline)
  - `docs/adr/0001-tech-stack.md` (C#/.NET LTS, WinUI 3, Windows service, SQLite for local storage, xUnit)
  - `docs/adr/0002-fixes-are-recipes-not-scripts.md`
- [x] Add GitHub files:
  - `.github/workflows/ci.yml`: runs on `windows-latest` for pushes and PRs. Steps: checkout, `actions/setup-dotnet` (LTS), `dotnet build --configuration Release`, `dotnet test --configuration Release`, and validation of every `library/recipes/*.yaml` against `library/schema/recipe.schema.json` (once the schema exists). Name the job `build`.
  - `.github/dependabot.yml`: weekly updates for `nuget` and `github-actions`
  - `.github/CODEOWNERS`: owner for everything; `src/Sherlog.Service/`, `library/` and `.github/workflows/` explicitly listed
  - Issue templates (`bug.yml`, `recipe-request.yml`, `feature.yml`) and `PULL_REQUEST_TEMPLATE.md` with a checklist: tests added, no personal data, user-facing text reviewed, undo implemented (for actions)
- [x] Create the GitHub repo and push:
  ```bash
  git add -A && git commit -m "chore: initial repository skeleton, plan and concept"
  gh repo create "$OWNER/sherlog" --public --source . --remote origin \
    --description "Windows PC health, explained: why it crashed, why it's slow, and whether it's safe. Free and open source." \
    --push
  gh repo edit "$OWNER/sherlog" --enable-issues --enable-discussions \
    --enable-squash-merge --delete-branch-on-merge \
    --add-topic windows --add-topic diagnostics --add-topic troubleshooting \
    --add-topic gaming --add-topic open-source --add-topic mcp
  ```
- [x] Create labels and milestones:
  ```bash
  for l in "phase:0-setup" "phase:1-prove-it" "phase:2-first-release" "phase:3-lag" "phase:4-agents-community" "phase:5-everyone" \
           "area:core" "area:cli" "area:service" "area:app" "area:mcp" "area:library" "area:docs" \
           "type:recipe" "security" "needs-owner"; do
    gh label create "$l" --force
  done
  for m in "Phase 0 — Setup" "Phase 1 — Prove it" "Phase 2 — First release" "Phase 3 — Lag" "Phase 4 — Agents and community" "Phase 5 — Everyone"; do
    gh api "repos/$OWNER/sherlog/milestones" -f title="$m" >/dev/null
  done
  ```
- [x] Create one issue per Phase 1 task (later phases get theirs at the start of each phase):
  ```bash
  gh issue create --title "Phase 1: Evidence readers for crash records" --label "phase:1-prove-it,area:core" --milestone "Phase 1 — Prove it" --body "See plan.md, Phase 1."
  # ...one per Phase 1 task
  ```
- [x] Turn on security features:
  ```bash
  gh api -X PATCH "repos/$OWNER/sherlog/code-scanning/default-setup" -f state=configured   # CodeQL default setup
  gh api -X PATCH "repos/$OWNER/sherlog" --input - <<'EOF'
  {"security_and_analysis":{"secret_scanning":{"status":"enabled"},"secret_scanning_push_protection":{"status":"enabled"}}}
  EOF
  ```
  If an API call is refused (feature unavailable for the account), note it in `docs/gates/phase-0.md` and move on.
- [x] Protect `main` after the first CI run succeeds:
  ```bash
  gh api -X PUT "repos/$OWNER/sherlog/branches/main/protection" --input - <<'EOF'
  {
    "required_status_checks": {"strict": true, "contexts": ["build"]},
    "enforce_admins": false,
    "required_pull_request_reviews": null,
    "restrictions": null,
    "allow_force_pushes": false,
    "allow_deletions": false
  }
  EOF
  ```

### Gate 0
- [x] `gh repo view "$OWNER/sherlog"` shows the repo, with description and topics
- [x] CI is green on `main`, and branch protection requires the `build` check
- [x] All Phase 1 issues exist in the "Phase 1 — Prove it" milestone
- [x] Evidence written to `docs/gates/phase-0.md`

---

## 7. Phase 1: Prove it (crash explainer CLI)

**Goal:** prove that Sherlog can explain crashes correctly. Build a command-line tool that reads the crash records Windows already keeps, matches them to recipes, and prints a plain-language cause with a confidence level. **No fixes are applied in this phase.**
**Skills:** full phase loop; `superpowers:dispatching-parallel-agents` for recipe research; `design:ux-copy` for diagnosis wording.

**Notes from Phase 0:**
- The solution file is `Sherlog.slnx` (the .NET 10 default format), not `Sherlog.sln`.
- The template files `Class1.cs`, `Program.cs` and `UnitTest1.cs` are still in place; replace them with real code and tests.
- Readers that use event logs or WMI need Windows-only APIs: switch those projects to the `-windows` target framework (ADR 0001).
- CI already validates `library/recipes/*.yaml` with `check-jsonschema` once `library/schema/recipe.schema.json` exists.
- Phase 1 issues: #2, #4–#10 in the "Phase 1 — Prove it" milestone.
- `main` is protected: all work goes through a branch and a pull request that passes the `build` check.

### Tasks

- [x] **Evidence model** (`Sherlog.Core`): `CrashEvent` (time, app name, app version, faulting module, module version, exception code, source, raw record ID) and `EvidenceSource` interfaces, so readers can be tested with fixtures.
- [x] **Evidence readers**, each tested with synthetic fixtures:
  - Application log: "Application Error" (event ID 1000), "Application Hang" (1002), Windows Error Reporting (1001)
  - System log: display driver resets ("Display", event ID 4101), blue screens ("BugCheck", 1001), unexpected shutdowns ("Kernel-Power", 41)
  - Reliability Monitor records (WMI class `Win32_ReliabilityRecords`)
  - Blue-screen dump summaries (bug-check code and faulting driver only; optional, if Debugging Tools for Windows is installed). *Not done in Phase 1: dump files need admin rights; the bug-check code comes from the BugCheck event instead (ADR 0003).*
- [x] **Recipe format v0** (`docs/recipe-format.md`, `library/schema/recipe.schema.json`, ADR). Start from Appendix B. In Phase 1 recipes only *diagnose*; their `fixes` are shown as suggestions, never run.
- [x] **Recipe engine**: load and validate YAML, match evidence, assign confidence (**Confirmed**, **Likely**, **Unclear**) using the rules in the recipe, and use repeat patterns as evidence (for example, the same faulting module 3 times in 7 days).
- [ ] **CLI commands** (`Sherlog.Cli`):
  - `sherlog crashes [--days 30]`: list crashes, grouped by cause
  - `sherlog explain --latest | <id>`: plain-language diagnosis with confidence and suggested fix
  - `sherlog collect --redact --out evidence.zip`: export an evidence bundle with user names, machine name and user-profile paths replaced
  - `sherlog eval <folder>`: run diagnoses on labeled bundles and report accuracy
- [ ] **Seed recipes:** research the most common gaming and app crash causes (graphics driver resets, overlays, corrupted game files, unstable memory or overclock, missing runtimes such as Visual C++ or DirectX, antivirus interference). Run research in parallel. Write at least **15 well-sourced recipes** first, then grow to **50**. Every recipe links its sources.
- [ ] **Wording pass** (`design:ux-copy`): every diagnosis reads clearly to a non-technical person.
- [ ] **Two kinds of test data, because crash causes are rarely known for certain:**
  - **Seeded crashes (exact ground truth):** in Windows Sandbox or a VM, cause known crashes on purpose with small test programs (for example an access violation in a named module, a missing Visual C++ runtime, a deliberately hung app). The cause is known exactly, so these test the readers and matching precisely.
  - **Field bundles (real-world truth):** **👤 OWNER** asks friends to run `sherlog collect --redact` on **10–20 Windows PCs** (gaming PCs preferred).
- [ ] **Two-layer `label.yaml`** written by `collect`:
  - **Owner section, for the PC's owner (a friend, not technical):** a plain-language checklist: what crashed, what they were doing, anything that changed recently (new driver, update, new app, overclock), what they tried, whether it fixed it, and whether it still happens. Multiple choice where possible, plus a free-text note.
  - **Investigator section, for Claude Code and the project owner:** `true_cause` (a recipe ID, or `unknown`), `evidence` (why we believe it) and `label_strength`: `confirmed-by-fix` (applying the matching fix stopped the crashes for 7 days or more), `owner-reported` (the owner's account fits the evidence) or `unknown`.
- [ ] Claude Code investigates each bundle, fills the investigator section from the owner's answers and the evidence, and asks the owner a follow-up a week later where a fix was tried, to upgrade labels to `confirmed-by-fix`.
- [ ] `sherlog eval` reports accuracy separately for seeded crashes and for field labels by `label_strength`. Bundles labeled `unknown` are counted in coverage but not in accuracy.

### Gate 1
- [ ] **100%** of seeded crashes are read and matched correctly
- [ ] At least **80%** of Confirmed and Likely diagnoses are correct on field bundles labeled `confirmed-by-fix` or `owner-reported`, with at least 10 such bundles
- [ ] **No more than 1** Confirmed diagnosis is wrong
- [ ] No more than **40%** of crashes end up Unclear
- [ ] `sherlog explain` answers in under 5 seconds on a typical PC
- [ ] Results and the list of misses written to `docs/gates/phase-1.md`. **If the gate fails, improve the recipes and repeat. Do not start Phase 2.**

---

## 8. Phase 2: First release

**Goal:** the first public version for gamers: the always-on recorder with the change timeline, crash notifications, the health verdict, and safe fixes with undo.
Split into five sub-phases, each run through the full phase loop.

### 2A: Fix engine and approved actions (security-critical: 👤 OWNER approves PRs)

- [ ] **Threat model** (`docs/threat-model.md`, STRIDE style) for the service, IPC, recipe library and updates. Review it with the owner before coding.
- [ ] Add `Sherlog.Service` (Windows service, runs elevated) and `tests/Sherlog.Service.Tests`.
- [ ] **IPC:** named pipe whose access list allows only the signed-in interactive user and SYSTEM. Every request is validated against the approved-actions list. The service accepts **recipe IDs from the installed, signed library only**, never recipe content from the client.
- [ ] Implement the **approved actions** from Appendix C, each with `Describe`, `Apply`, `Verify` and `Undo`.
- [ ] **Undo records** saved to SQLite **before** `Apply`: previous driver package and version, old setting values, disabled startup entries.
- [ ] **Restore points:** attempt one before moderate and advanced fixes when System Protection is on. Never rely on it alone: Windows skips a new restore point if one was made in the last 24 hours.
- [ ] **Sandbox test harness:** Windows Sandbox `.wsb` configs (and an optional Hyper-V script) in `sandbox/` that install a build, apply each action, verify it, undo it, and check the system is back to its earlier state.
- **Gate 2A:** every action passes apply, verify and undo tests **inside the sandbox**; the threat model is reviewed by the owner; no action can run outside the allowlist (covered by tests).

### 2B: Recorder service and change timeline

- [ ] Real-time subscriptions to the crash sources from Phase 1 (event-log watchers instead of polling).
- [ ] **Change timeline** from snapshot differences and events: Windows Update history, driver installs and updates, installed apps (uninstall registry keys), startup entries (Run keys, Startup folder, logon scheduled tasks, auto-start services).
- [ ] Local storage in SQLite with a retention limit (default 90 days) and a size cap.
- [ ] Link crashes to changes ("crashes began 2 days after the graphics driver update").
- [ ] **Performance budget tests:** under 1% average CPU, under 100 MB memory; pause heavy work while a full-screen game runs.
- **Gate 2B:** budgets met over a 24-hour run; **👤 OWNER** runs a game benchmark with and without the service and confirms the frame-rate drop is under 1%.

### 2C: Health check

- [ ] Security: Microsoft Defender status (WMI `MSFT_MpComputerStatus`), firewall profiles, Windows Update status, Secure Boot, TPM, unknown startup entries.
- [ ] Windows support status, including Windows 10 out of support without Extended Security Updates.
- [ ] Hardware: drive wear and health (storage reliability counters, SMART), battery capacity on laptops.
- [ ] Integrity: `sfc` and `DISM` as user-started, long-running checks (moderate risk, in the service).
- [ ] One plain verdict ("Healthy, with 1 thing worth a look"), with each part expandable.
- **Gate 2C:** every check has fixture-based tests, and verdict wording passes `design:ux-copy`.

### 2D: The app

- [ ] Run `frontend-design:frontend-design` to set the visual direction: calm, honest, clear, never alarming. Record it in an ADR.
- [ ] Add `Sherlog.App` (WinUI 3). **👤 OWNER:** if the WinUI project cannot be created from the command line, ask the owner to create it in Visual Studio.
- [ ] Screens: **Home** (verdict and three doors: *Something crashed*, *It's slow*, *Am I safe?*), **Crash detail**, **Fix confirmation** (what will change, risk, time, undo), **History** (every change with Undo), **Settings** (what runs automatically, sharing, do-not-disturb while gaming).
- [ ] Crash notifications through Windows app notifications: "Valorant just crashed. We know why. Fix it?"
- [ ] English and Arabic from the start, with right-to-left layout tested.
- [ ] `design:accessibility-review` and `design:design-critique` on screenshots of every screen; fix the findings.
- **Gate 2D:** accessibility review passes; **👤 OWNER** watches 3 non-technical people use it and reports where they got stuck.

### 2E: Packaging and release

- [ ] **👤 OWNER: code signing.** Choose a signing route (for example Azure Trusted Signing, or a free open-source signing program such as SignPath Foundation) and set up the account. Record it in an ADR.
- [ ] MSIX packaging and a signed release workflow (`.github/workflows/release.yml`). Release workflow changes need owner approval.
- [ ] Release: `gh release create v0.1.0 --generate-notes` with signed artifacts and release notes (`anthropic-skills:humanizer` pass).
- [ ] winget manifest pull request to `microsoft/winget-pkgs`.
- [ ] **👤 OWNER:** Microsoft Store submission (needs the owner's Partner Center account).
- [ ] **👤 OWNER:** submit signed builds to Microsoft for malware analysis early, to avoid false positives.
- **Gate 2E:** a clean Windows 11 machine (sandbox) installs from the release, works offline, and uninstalls cleanly.

---

## 9. Phase 3: Lag

**Goal:** answer "Why is my PC lagging?" by naming the kind of lag and its cause.

- [ ] **"It's slow right now" hotkey:** capture about 30 seconds of Windows event tracing (`Microsoft.Diagnostics.Tracing.TraceEvent`) and system counters.
- [ ] **Lag classifier** for the six kinds in the concept: frame stutter, overheating, resource hog, background Windows work, driver latency (DPC/ISR), slow or failing drive.
- [ ] **Frame times** from outside the game (for example PresentMon, which is open source; check its license and how to integrate it in an ADR).
- [ ] **Temperatures:** choose the source in an ADR. Avoid kernel drivers that anti-cheat or security tools flag (some older hardware-monitoring libraries relied on the WinRing0 driver, which security tools have flagged). Prefer WMI or vendor interfaces.
- [ ] **Per-PC baselines** for boot time, frame rate and temperatures; "slower than usual since Tuesday."
- [ ] **👤 OWNER:** capture 10 labeled lag situations on real PCs for testing.
- **Gate 3:** at least 8 of the 10 labeled captures are classified correctly, and capture overhead is acceptable while gaming.

---

## 10. Phase 4: Agents and community

**Goal:** Sherlog as an MCP server for AI agents, and an open, safe community fix library.

### MCP server (`anthropic-skills:mcp-builder`)
- [ ] Add `Sherlog.Mcp` using the official C# MCP SDK (`ModelContextProtocol` NuGet package; check the current version), stdio transport.
- [ ] Tools: `get_health`, `list_crashes`, `get_crash_details`, `get_change_timeline`, `capture_lag_trace`, `run_integrity_check`, `propose_fix`, `apply_fix`, `undo_fix`, `get_fix_status`.
- [ ] **Approval rule:** `apply_fix` runs safe fixes directly. For moderate and advanced fixes it returns `pending_approval`, and Sherlog's own window asks the person. The agent can never approve its own risky fix.
- [ ] Every agent action appears in History with Undo.
- [ ] Agent-proposed fixes that work can be exported as **draft recipes** for human review.
- [ ] Docs: how to connect Claude Code and other MCP clients.

### Community library
- [ ] Recipe contribution flow: schema validation and a dry-run check in CI, plus a sandbox test by a maintainer.
- [ ] Two reviews required on `library/` once there are 2 or more maintainers (update branch protection then).
- [ ] **Signed library bundles** (choose the signing tool in an ADR) and **staged rollout** (1%, then 10%, then everyone) with automatic pull-back when failure reports rise.
- [ ] **👤 OWNER:** opt-in reporting backend (hosting choice). Shares only crash signatures, driver versions and fix outcomes, never dumps or personal data. Fixes are ranked by real success rates.
- **Gate 4:** an agent diagnoses and fixes a seeded problem in the sandbox end to end, and is blocked from approving a risky fix itself; a community recipe goes from pull request to staged rollout.

---

## 11. Phase 5: Everyone

- [ ] More languages (community translations), with RTL layout checked for each RTL language.
- [ ] **Family mode:** help a relative's PC from your own, with secure pairing and the relative's consent on every fix. Write an ADR and update the threat model first.
- [ ] **Hardware-life forecasts:** drive wear and battery trends ("plan a replacement in the next few months").
- [ ] Full `design:accessibility-review` again, and an **outside security review** before 1.0 (👤 OWNER arranges it).
- **Gate 5:** version 1.0 released after the security review findings are fixed.

---

## 12. Owner checkpoints at a glance

| Phase | What the owner does |
|---|---|
| 0 | Answer the decision questions |
| 1 | Ask friends to run `sherlog collect` on 10–20 PCs and fill in the plain-language checklist; answer follow-ups a week later |
| 2A | Review the threat model; approve security-critical PRs |
| 2B | Run a game benchmark with and without the recorder |
| 2D | Create the WinUI project in Visual Studio if needed; watch 3 people use the app |
| 2E | Set up code signing, the Microsoft Store account, and malware submissions |
| 3 | Capture 10 labeled lag situations |
| 4 | Choose hosting for opt-in reporting |
| 5 | Arrange the outside security review |

---

## 13. Definition of done (every task)

- Tests written first and passing, in CI as well as locally
- No personal data in code, fixtures, logs or commits
- User-facing text is plain, honest and shows confidence where relevant
- Every fix action has Describe, Apply, Verify and Undo, tested in the sandbox
- Docs updated (ADR, glossary, README or recipe docs as needed)
- `superpowers:verification-before-completion` run, with evidence linked in the PR

---

## Appendix A: `CLAUDE.md` template

```markdown
# Sherlog: rules for Claude Code

Read plan.md, docs/concept.md and docs/adr/ before starting any work.

## Never
- Apply fixes, change drivers, edit the registry or change system settings on this development PC. Test fix actions only in Windows Sandbox or a Hyper-V VM.
- Disable or weaken any Windows security feature, even temporarily.
- Inject code into or hook games or other processes.
- Commit crash dumps, real event logs, user names, machine names or user-profile paths.
- Add a "run command" or "run script" fix action.
- Merge PRs that touch src/Sherlog.Service/, the fix engine, IPC, the approved-actions list, signing or release workflows without the owner's approval.

## Always
- Ask before running anything that needs admin rights on this PC.
- Write the failing test first.
- Save the undo record before applying any fix.
- Use plain language with a confidence level in anything users read.
- Follow the phase loop in plan.md, and tick plan.md checkboxes as tasks finish.

## Commands
- Build: dotnet build
- Test: dotnet test
- Validate recipes: (added in Phase 1)
```

---

## Appendix B: Recipe format v0 (starting point)

```yaml
id: gpu-nvidia-driver-reset
version: 1
title: NVIDIA graphics driver stopped responding
category: crash
references:
  - https://learn.microsoft.com/en-us/windows-hardware/drivers/display/timeout-detection-and-recovery

match:
  any:
    - source: system-log
      provider: Display
      event_id: 4101
      message_contains: nvlddmkm
    - source: bugcheck
      faulting_module: nvlddmkm.sys

confidence:
  default: likely
  confirmed_when:
    - occurrences_within_days: { count: 3, days: 7 }

diagnosis:
  summary: "Your NVIDIA graphics driver stopped responding, and Windows had to restart it."
  details: "This usually comes from a recent driver update, an unstable overclock, or overheating."

fixes:
  - id: rollback-recent-display-driver
    risk: moderate
    when:
      change_timeline: { kind: driver_update, device_class: Display, within_days: 14 }
    explain: "Your graphics driver was updated recently. Going back to the previous version often stops these crashes."
    actions:
      - action: rollback_driver
        params: { device_class: Display }
    verify:
      no_recurrence_days: 7

  - id: guide-clean-driver-install
    risk: advanced
    explain: "Reinstall the graphics driver cleanly from NVIDIA's website."
    actions:
      - action: show_guidance
        params: { guide: nvidia-clean-install }
```

---

## Appendix C: Approved actions v0

The fix engine can only run actions on this list. Adding an action needs an ADR, a threat-model update and owner approval.

| Action | Default risk | Undo |
|---|---|---|
| `show_guidance`: show steps the person does themselves | Safe | Not needed |
| `open_settings_page`: open a Windows Settings page (`ms-settings:` links only) | Safe | Not needed |
| `open_url`: open an https link on an allowlisted domain | Safe | Not needed |
| `verify_game_files`: open the launcher's file check (for example `steam://validate/<appid>`) | Safe | Not needed |
| `end_process`: end a stuck, non-system process the person names | Safe | Not reversible; ask first if unsaved work is possible |
| `disable_startup_entry` | Moderate | Re-enable the entry |
| `rollback_driver`: go back to the previous driver package | Moderate | Reinstall the newer package |
| `set_allowlisted_setting`: change a setting on a reviewed allowlist only | Moderate | Restore the saved old value |
| `run_system_check`: run `sfc` or `DISM` repair | Moderate | Not needed (Windows repair only) |
