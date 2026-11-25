# ADR 0007: Seeded crashes run on GitHub's Windows runners first

- **Status:** Accepted
- **Date:** 2026-11-25

Seeded crashes are small C programs (for example one that crashes inside a DLL with a known name) built and crashed on GitHub Actions `windows-latest` runners. The runner then runs `sherlog collect`, and `sherlog eval` checks every seeded crash against its known cause. The plan named Windows Sandbox or a VM; we use CI runners first because:

- The development PC has the C++ compiler but not the Windows SDK, so it cannot build native test programs without an admin install. Runners have both.
- A runner is a throwaway Windows VM, so crashing programs on purpose there never touches the development PC, and the test runs on every pull request instead of by hand.

Windows Sandbox stays the place for seeded cases a runner cannot reproduce, such as a missing Visual C++ runtime (runners already have it installed) or a hung window that needs an interactive desktop. Those cases get `.wsb` configs in `sandbox/`, and the programs they run are taken from the CI build.

## Consequences

- If runners turn out not to record a seeded crash the way a normal PC does (for example Windows Error Reporting set up differently), that case moves to the sandbox and the gap is written down in `docs/gates/phase-1.md`.
- The seeded-crash workflow only crashes its own test programs, filters evidence by their names, and never changes runner settings.
