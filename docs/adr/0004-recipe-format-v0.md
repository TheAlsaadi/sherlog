# ADR 0004: Recipe format v0, matching and confidence

- **Status:** Accepted
- **Date:** 2026-11-25

Recipes are YAML, validated against one JSON Schema (`library/schema/recipe.schema.json`) that both CI and the engine use. The format starts from plan.md Appendix B; the full reference is `docs/recipe-format.md`.

Decisions that a reader might not expect:

- **Matching is field-based, never free code.** A match condition names an evidence `source` and constrains typed fields (`app_name`, `faulting_module`, `exception_code`, `bugcheck_code`, `provider`, `event_id`, `message_contains`). All fields in one condition must hold for the same crash. Text fields compare case-insensitively; `app_name` and `faulting_module` accept `*` wildcards. A recipe lists conditions under `match.any`; one matching condition is enough. v0 has no `all` across different records: "a display reset followed by a crash" needs the change timeline and correlation rules of Phase 2B.
- **Recipes test themselves.** Every recipe has at least one entry under `examples`: the evidence fields of a crash it must explain. A library test diagnoses every example against the **whole** library and fails if any example resolves to a different recipe, so an overlapping new recipe is caught in its pull request.
- **Most specific recipe wins.** When several recipes match one crash, the engine picks the one whose matching condition constrains the most fields, then the lower `priority` number (default 100), then the recipe ID. Broad fallback recipes (for example "the game crashed in its own files") set a high `priority` so specific ones beat them.
- **Confidence is earned, never assumed.** A recipe's `confidence.default` may be `likely` or `unclear`, never `confirmed`. A diagnosis becomes **Confirmed** only when the recipe's default is `likely` **and** a `confirmed_when` rule holds for that crash group, such as `occurrences_within_days: {count: 3, days: 7}` (`count` at least 3). An `unclear` recipe never becomes Confirmed: repeats make a problem more certain, not its cause. A crash no recipe matches is **Unclear**, and Sherlog still says what it knows (the app, the failing file, the error code) and what to try next.
- **Fixes are suggestions in Phase 1.** The schema already restricts `actions[].action` to the approved-actions list (plan.md Appendix C) so recipes written now stay valid when the fix engine arrives. Nothing runs them in Phase 1. `when` conditions that need the change timeline are recorded but not evaluated until Phase 2B.
- **Every recipe needs at least one `references` link.**

We kept the format small on purpose: every field added now has to be supported forever by community recipes.
