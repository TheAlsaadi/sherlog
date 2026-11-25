# ADR 0006: Labels are per crash group, in a two-layer label file

- **Status:** Accepted
- **Date:** 2026-11-25

Each bundle has one `label.yaml` with two sections (plan.md, Phase 1):

- **Owner answers**, asked by `sherlog collect` as a short multiple-choice conversation in the terminal (what crashed, what they were doing, recent changes, what they tried, whether it helped, whether it still happens, plus a free-text note). Answers are written to the file; if the friend skips the questions, the file keeps the questions with empty answers so they can be filled in by hand.
- **Investigator labels**, a list with one entry per **crash group** in the bundle: `crash_group` (the group's ID as `sherlog crashes` prints it), `true_cause` (a recipe ID or `unknown`), `evidence`, and `label_strength` (`seeded`, `confirmed-by-fix`, `owner-reported` or `unknown`).

We label crash groups rather than whole bundles because one PC often has several unrelated crashes: a graphics driver reset and a game that crashes in its own files are two answers, and one bundle-level label would score one of them as wrong.

`sherlog eval` scores each labeled group, reports seeded and field results separately, splits field results by `label_strength`, and counts `unknown` labels toward coverage only. A field bundle counts toward Gate 1's "at least 10 bundles" when it has at least one group labeled `confirmed-by-fix` or `owner-reported`.

Owner answers are asked interactively because editing a YAML file is a bigger hurdle for a non-technical friend than answering six questions.
