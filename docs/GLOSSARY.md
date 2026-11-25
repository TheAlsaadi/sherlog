# Glossary

The words Sherlog uses in its code, docs and recipes. Keep this file current when a term is added or its meaning changes.

**Evidence**
A fact Windows has already recorded that Sherlog reads, such as an Application Error event, a display driver reset, a blue-screen record or a Reliability Monitor entry. Evidence is read-only and stays on the PC. Each piece keeps a reference to its original record.

**Evidence source**
A reader for one kind of evidence (for example the Application event log or Reliability Monitor). Every source can be swapped for synthetic fixtures in tests.

**Diagnosis**
Sherlog's plain-language explanation of what caused a problem, produced by matching evidence against recipes. A diagnosis always has a confidence level and may suggest fixes.

**Confidence**
How sure a diagnosis is. One of three levels:
- **Confirmed**: clear evidence and a known fix.
- **Likely**: a strong pattern, worth trying the fix.
- **Unclear**: here is what we know and what to try next.

A confidently wrong answer does more harm than an honest "Unclear".

**Recipe**
A declarative YAML entry in the community library (`library/recipes/`). It says what evidence to match, how to assign confidence, how to explain the cause, and which fixes to offer. A recipe never contains code or commands.

**Action**
One step a fix can take, chosen from the fixed **approved-actions list** (for example `rollback_driver` or `disable_startup_entry`). Every action has four parts: `Describe` (dry run), `Apply`, `Verify` and `Undo`. Adding an action needs an ADR, a threat-model update and owner approval.

**Fix**
A named set of actions inside a recipe, with a risk level (**Safe**, **Moderate** or **Advanced**), a plain explanation, and a way to check afterwards that the problem went away.

**Undo record**
The exact information needed to reverse a fix (the previous driver package, the old setting value, the disabled startup entry), saved **before** the fix is applied.

**Change timeline**
The recorder's ordered list of changes to the PC: driver installs and updates, Windows updates, installed apps, startup entries and changed settings. Lining changes up against problems is how Sherlog finds causes like "crashes began 2 days after the graphics driver update".
