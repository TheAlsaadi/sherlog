# Sherlog: rules for Claude Code

Read plan.md, docs/concept.md and docs/adr/ before starting any work.

## Never
- Apply fixes, change drivers, edit the registry or change system settings on this development PC. Test fix actions only in Windows Sandbox or a Hyper-V VM.
- Disable or weaken any Windows security feature, even temporarily.
- Inject code into or hook games or other processes.
- Commit crash dumps, real event logs, user names, machine names or user-profile paths.
- Add a "run command" or "run script" fix action.
- Merge PRs that touch src/Sherlog.Service/, the fix engine, IPC, the approved-actions list, signing or release workflows without the owner's approval.
- Add Claude as a co-author or contributor: no `Co-Authored-By: Claude` lines in commits and no "Generated with Claude Code" footer in PRs (owner decision, Phase 0).

## Always
- Ask before running anything that needs admin rights on this PC.
- Write the failing test first.
- Save the undo record before applying any fix.
- Use plain language with a confidence level in anything users read.
- Follow the phase loop in plan.md, and tick plan.md checkboxes as tasks finish.

## Commands
- Build: dotnet build
- Test: dotnet test
- Validate recipes: dotnet run --project src/Sherlog.Cli -- recipes check
