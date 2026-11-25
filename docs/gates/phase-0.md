# Gate 0: Repository setup

- **Date:** 2026-11-25
- **Result:** Passed

## Owner decisions

| Decision | Answer |
|---|---|
| GitHub owner | Personal account `TheAlsaadi` |
| Repository name and visibility | `sherlog`, public |
| Code license | MIT, copyright "Sherlog contributors" |
| Recipe library license | CC BY 4.0 |
| Claude Code may merge its own non-security PRs after CI and code review | Yes, with no Claude co-author or contributor listed on commits or PRs |
| Windows Sandbox / Hyper-V | Windows 11 Pro; Windows Sandbox is installed (`WindowsSandbox.exe` present). Hyper-V management service not present. Sandbox is enough for Phase 2A. |

## Tooling

| Tool | Version |
|---|---|
| GitHub CLI | signed in, scopes `repo`, `workflow`, `read:org`, `gist` |
| Git | 2.50.0.windows.2 |
| .NET SDK | 10.0.401 (LTS) |

## Gate checks

### 1. Repo exists with description and topics

`gh repo view TheAlsaadi/sherlog`:

```json
{"url":"https://github.com/TheAlsaadi/sherlog","visibility":"PUBLIC",
 "description":"Windows PC health, explained: why it crashed, why it's slow, and whether it's safe. Free and open source.",
 "topics":["diagnostics","gaming","mcp","open-source","troubleshooting","windows"],"discussions":true}
```

Issues, discussions, squash merge and delete-branch-on-merge are enabled.

### 2. CI green on `main`, and branch protection requires `build`

- CI run on the initial commit `7204b97`: **success** ([run 45606454173](https://github.com/TheAlsaadi/sherlog/actions/runs/45606454173)).
- Local check before pushing: `dotnet build --configuration Release` gave 0 warnings and 0 errors; `dotnet test --configuration Release` passed 2 of 2 template tests.
- Branch protection on `main`:

```json
{"checks":["build"],"strict":true,"force_push":false,"deletions":false}
```

### 3. Phase 1 issues in the "Phase 1 — Prove it" milestone

| Issue | Task |
|---|---|
| [#2](https://github.com/TheAlsaadi/sherlog/issues/2) | Evidence model (CrashEvent, EvidenceSource) |
| [#4](https://github.com/TheAlsaadi/sherlog/issues/4) | Evidence readers for crash records |
| [#5](https://github.com/TheAlsaadi/sherlog/issues/5) | Recipe format v0 and JSON schema |
| [#6](https://github.com/TheAlsaadi/sherlog/issues/6) | Recipe engine (load, validate, match, confidence) |
| [#7](https://github.com/TheAlsaadi/sherlog/issues/7) | CLI commands (crashes, explain, collect, eval) |
| [#8](https://github.com/TheAlsaadi/sherlog/issues/8) | Seed recipes (15 first, then 50) |
| [#9](https://github.com/TheAlsaadi/sherlog/issues/9) | Wording pass on every diagnosis |
| [#10](https://github.com/TheAlsaadi/sherlog/issues/10) | Field test on 10–20 PCs (owner) |

Labels (16 phase, area and type labels) and milestones for Phases 0 to 5 were created.

### 4. Evidence written

This file.

## Security features

| Feature | Result |
|---|---|
| CodeQL default setup | Configured (first analysis started on 2026-11-25) |
| Secret scanning | Enabled |
| Secret scanning push protection | Enabled |
| Private vulnerability reporting | Enabled (used by `SECURITY.md`) |
| Dependabot alerts and security updates | Enabled |
| Dependabot version updates | `nuget` and `github-actions`, weekly |

No API call was refused.

## Notes and deviations

- **Solution file is `Sherlog.slnx`**, not `Sherlog.sln`. The .NET 10 SDK creates the XML solution format by default. Recorded in ADR 0001.
- **`docs/concept.md`** was moved from the repository root into `docs/`, as the target layout expects.
- **Template code** (`Class1.cs`, `Program.cs`, `UnitTest1.cs`) is still in place so the build and test pipeline has something to run. Phase 1 replaces it.
- **GitHub Actions versions:** CI started on `actions/checkout@v5` and `actions/setup-dotnet@v5`. Dependabot immediately opened PRs to move to v7 and v6 (#3 and #1).
- `CLAUDE.md` has an extra rule beyond Appendix A: no Claude co-author or contributor lines (owner decision).
