# ADR 0002: Fixes are recipes, not scripts

- **Status:** Accepted
- **Date:** 2026-11-25

## Context

Sherlog applies fixes to people's PCs through a service with administrator rights. Fixes come from a community library that anyone can contribute to. If a library entry could run arbitrary commands, one malicious or careless pull request could reach every PC that installs the library, and the admin service would be an easy way to take over a machine.

## Decision

- Each fix is a **declarative recipe** in YAML. A recipe can only combine actions from the fixed **approved-actions list** (plan.md, Appendix C) with typed parameters.
- The fix engine has **no** "run command", "run script", "run PowerShell" or similar action, and none will be added.
- Every action implements four parts: `Describe` (dry run), `Apply`, `Verify` and `Undo`. The undo record is saved **before** `Apply` runs.
- Adding an action to the list needs a new ADR, a threat-model update and owner approval.
- The service accepts **recipe IDs from the installed, signed library only**, never recipe content sent by a client.

## Consequences

- Every fix can be reviewed by reading its YAML, tested in a sandbox, and undone automatically.
- Some fixes that need custom logic can't be expressed as recipes. They either become a new reviewed action, or a `show_guidance` step the person does themselves.
- Schema validation in CI can reject recipes that use unknown actions or parameters before review.
