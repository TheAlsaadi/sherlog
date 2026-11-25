# Sherlog

Sherlog explains why a Windows PC crashed, why it is slow and whether it is safe, then offers fixes that can be undone. These are the words its code, docs and recipes use.

## Evidence and diagnosis

**Evidence**:
A fact Windows has already recorded that Sherlog reads, such as an Application Error event, a display driver reset, a blue-screen record or a Reliability Monitor entry. Evidence stays on the PC unless the person exports it.
_Avoid_: Log data, telemetry

**Evidence source**:
Where a kind of evidence comes from: a live log or WMI class on this PC, or an evidence bundle.
_Avoid_: Provider (that word means the event-log provider)

**Crash**:
One recorded failure: an app crash, an app hang, a graphics driver reset, a blue screen or an unexpected shutdown.
_Avoid_: Incident, error

**Crash group**:
Crashes that share one signature (the same app failing in the same file with the same error, or the same driver failing), treated as one problem.
_Avoid_: Cluster, bucket

**Diagnosis**:
Sherlog's plain-language explanation of what caused a crash group, with a confidence level and suggested fixes.
_Avoid_: Verdict (that word is for the health check), result

**Confidence**:
How sure a diagnosis is: **Confirmed** (clear evidence and a known fix), **Likely** (a strong pattern, worth trying the fix) or **Unclear** (here is what we know and what to try next).
_Avoid_: Score, probability

## Recipes and fixes

**Recipe**:
A reviewed library entry that says which evidence points to a cause, how confident a match is, how to explain it, and which fixes to offer. It never contains code or commands.
_Avoid_: Rule, script, signature

**Fix**:
A named set of actions inside a recipe, with a risk level (**Safe**, **Moderate** or **Advanced**) and a way to check afterwards that the problem went away.
_Avoid_: Repair, solution

**Action**:
One step a fix can take, chosen from the fixed approved-actions list.
_Avoid_: Command, operation

**Undo record**:
The exact information needed to reverse a fix, saved before the fix is applied.

**Change timeline**:
The ordered list of changes made to the PC (drivers, Windows updates, installed apps, startup entries, settings), lined up against crashes to find causes.

## Testing accuracy

**Evidence bundle**:
A redacted package of one PC's raw crash records, a short system summary and a label file, made so a diagnosis can be re-run elsewhere.
_Avoid_: Dump, export, report

**Redaction**:
Replacing anything that identifies a person or PC (user names, PC name, account IDs, profile paths) with fixed placeholders before evidence leaves the PC.
_Avoid_: Anonymization (redaction does not promise full anonymity)

**Seeded crash**:
A crash caused on purpose by a test program on a throwaway machine, so its cause is known exactly.

**Field bundle**:
An evidence bundle from a real person's PC, where the cause has to be worked out.

**Owner answers**:
The PC owner's plain-language account of a crash: what crashed, what they were doing, what changed, what they tried and whether it helped.

**Label**:
An investigator's conclusion about one crash group: its true cause (a recipe or `unknown`), the evidence for it, and its label strength.

**Label strength**:
How well a label's cause is known: **seeded** (caused on purpose), **confirmed-by-fix** (the matching fix stopped the crashes for at least 7 days), **owner-reported** (the owner's account fits the evidence) or **unknown**.
