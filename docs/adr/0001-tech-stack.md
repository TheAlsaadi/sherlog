# ADR 0001: Technology stack

- **Status:** Accepted
- **Date:** 2026-11-25

## Context

Sherlog reads Windows event logs, WMI (Reliability Monitor, Defender status, storage health) and Windows event tracing, runs a privileged background service, and shows a native Windows 11 app. It must be light while games run, work offline, and be easy for contributors to build.

## Decision

- **Language and runtime:** C# on .NET 10, the current LTS release. Shared settings live in `Directory.Build.props`: nullable reference types on, implicit usings on, warnings treated as errors. Projects that need Windows-only APIs use the `-windows` target framework.
- **App:** WinUI 3 (Windows App SDK), added in Phase 2D.
- **Recorder and fix executor:** a .NET Windows service, added in Phase 2A. It runs elevated; the app and CLI do not.
- **Local storage:** SQLite, for the change timeline, crash history and undo records.
- **Tests:** xUnit.
- **Solution file:** `Sherlog.slnx`, the XML solution format that the .NET 10 SDK creates by default (plan.md's layout shows `Sherlog.sln`; the content is the same).

## Consequences

- .NET has first-class access to event logs, WMI and event tracing, so most evidence readers need no native code.
- WinUI 3 limits the app to Windows 10 1809 and later, which matches Sherlog's audience.
- WinUI 3 projects are hard to create purely from the command line; the owner may need to create that project in Visual Studio (Phase 2D).
- .NET 10 LTS is supported until November 2028. Moving to the next LTS will need its own ADR.
