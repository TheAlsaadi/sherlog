# Contributing to Sherlog

Thanks for helping. Sherlog is early, so the build and the recipe format will change. This guide covers what works today.

## Build and test

You need Windows 10 or 11 and the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```powershell
git clone https://github.com/TheAlsaadi/sherlog.git
cd sherlog
dotnet build
dotnet test
```

Warnings are treated as errors, so a clean build has zero warnings.

## How we work

- **Tests first.** Write a failing test, then the code that makes it pass.
- **Small pull requests** with conventional commit messages: `feat:`, `fix:`, `test:`, `docs:`, `chore:`.
- **Plain language** in anything a user reads. No jargon, no scare tactics, and every diagnosis states how confident it is.
- Fill in the pull request checklist. CI must pass before anything is merged.

## Never commit personal data

Do not commit real crash dumps, raw event logs, user names, machine names or paths from a user profile. Test fixtures must be synthetic, or redacted with `sherlog collect --redact` (available from Phase 1).

## Submitting a recipe

Recipes live in `library/recipes/` as YAML files. Each one describes a crash or problem signature, how to recognise it, how confident a match is, and which fixes to suggest. The format is being finalised in Phase 1 and will be documented in `docs/recipe-format.md`, with a schema in `library/schema/recipe.schema.json`.

A good recipe:

1. Links its sources (vendor documentation, Microsoft Learn, well-known bug reports).
2. Matches on evidence Windows records, such as an event ID plus a faulting module.
3. Uses only actions from the approved-actions list. Recipes can't run commands or scripts, and pull requests that try will be closed.
4. Explains the cause and the fix in words a non-technical person understands.

Until the schema lands, open a **Recipe request** issue with the crash signature and your sources, and we'll turn it into a recipe together.

Recipes are licensed under [CC BY 4.0](library/LICENSE). Code is licensed under [MIT](LICENSE).

## Security issues

Please don't open public issues for security problems. See [SECURITY.md](SECURITY.md).
