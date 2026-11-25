# ADR 0008: Libraries for Phase 1

- **Status:** Accepted
- **Date:** 2026-11-25

- **System.CommandLine 3.0** for the `sherlog` command: Microsoft's own parser, gives consistent help and error text, and is now stable. A hand-written parser was the alternative; four commands with a few options would fit, but help and error wording would drift.
- **YamlDotNet** for recipes and label files: the standard .NET YAML library.
- **JsonSchema.Net** to validate recipes inside the engine against the same `recipe.schema.json` that CI uses, so there is one definition of a valid recipe.
- **System.Diagnostics.EventLog** (`EventLogReader`) for event logs and **System.Management** for WMI (Reliability Monitor, basic hardware facts).

All diagnosis output is plain text by default; every command also takes `--json` so `eval`, tests and the future MCP server can read results without parsing prose.
