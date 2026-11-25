# ADR 0003: Readers parse raw Windows records, and bundles carry those raw records

- **Status:** Accepted
- **Date:** 2026-11-25

Every evidence reader is split in two: a thin **record source** that fetches raw records (event-log entries as their XML, Reliability Monitor rows as key/value pairs), and a **parser** that turns raw records into evidence. Live sources read this PC; bundle sources read an evidence bundle. `sherlog collect` writes the **redacted raw records** into the bundle, not Sherlog's parsed evidence.

We chose this because Phase 1 is an accuracy experiment that will repeat: when Gate 1 fails, we fix parsers and recipes and re-run `sherlog eval` on the same bundles. If bundles held only parsed evidence, every parser fix would need friends to collect again. Parsers also become testable with synthetic XML fixtures on any machine.

## Consequences

- `Sherlog.Core` and `Sherlog.Cli` (and their tests) target `net10.0-windows`, because live sources use Windows-only APIs (event log and WMI). The shared default in `Directory.Build.props` stays `net10.0` for projects that don't need Windows.
- Bundles are larger than a parsed summary, and raw records can hold more personal data, so redaction (ADR 0005) runs on raw records before anything is written.
- Blue-screen dump files are **not** read in Phase 1: Windows lets only administrators read them, and parsing them needs Debugging Tools. The bug-check code comes from the System log's BugCheck event instead.
