# Phase 1: Crash Explainer CLI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A `sherlog` command-line tool that reads the crash records Windows already keeps, groups them, matches them to recipes, explains the cause in plain language with a confidence level, exports redacted evidence bundles, and measures its own accuracy.

**Architecture:** Raw records (event XML plus rendered message, Reliability Monitor rows) come from an `IRecordSource` (live PC or bundle). Pure parsers turn them into `CrashEvent`s, which are de-duplicated and grouped into `CrashGroup`s. A `Diagnoser` matches each group against the recipe library (most specific recipe wins, confidence earned by repeat rules). The CLI wires these together; `collect` writes redacted raw records to a zip; `eval` re-runs diagnosis on bundles and scores it against labels.

**Tech Stack:** C# / .NET 10 (`net10.0-windows`), xUnit, System.CommandLine 3.0.0, YamlDotNet 18.1.0, JsonSchema.Net 9.7.0, System.Diagnostics.EventLog 10.0.15, System.Management 10.0.15. Seeded-crash programs in C, built with MSVC on GitHub `windows-latest`.

**Spec:** `plan.md` §7 (Phase 1) and Appendix B; ADRs `docs/adr/0003`–`0008`; vocabulary in `docs/GLOSSARY.md`.

## Global Constraints

- Tests first, every time. `TreatWarningsAsErrors` stays on.
- No real event logs, dumps, user names, PC names or profile paths in fixtures or commits. Fixtures are hand-written synthetic XML/JSON.
- Never change settings on the development PC. Reading event logs and WMI needs no admin rights; nothing in this phase may need them.
- No fix is ever executed. Fixes print as suggestions only.
- User-facing text: plain language, no jargon, no scare words, always states confidence (Confirmed / Likely / Unclear).
- Crash dumps are never read (ADR 0003).
- Redaction cannot be switched off (ADR 0005).
- Commit messages: conventional (`feat:`, `test:`, `docs:`, `chore:`, `fix:`), no Claude co-author lines.
- One PR per task group (A–E below), merged after CI and the code-review skill.

## Review Focus

1. **Positional `<Data>` elements.** Some events (Application Hang 1002, WER 1001, Display 4101) carry unnamed `<Data>`; named ones can also be missing on older builds. Parsers must read named data first, positional second. → tests in Task 2 and 3.
2. **Non-English Windows.** Rendered messages are localized; any field that exists in structured data must come from structured data, never from the message. → `DisplayReset_driver_name_comes_from_data_not_message` in Task 3.
3. **A PC with no crashes, or a source that can't be read.** `crashes` / `explain --latest` print a calm "no crashes found" line and exit 0; an unreadable source (Reliability service off, access denied) prints one warning and the other sources still work. → Task 4 and Task 8.
4. **A broken recipe file.** The engine skips the invalid file with one warning naming the file and the first error, and still diagnoses with the rest; `sherlog recipes check` fails with exit code 1. → Task 6 and Task 8.
5. **User names inside app paths and messages.** WER and Reliability records contain full paths like `C:\Users\<name>\AppData\...`; no original user name, PC name or account SID may survive anywhere in a bundle. → `Bundle_contains_no_original_identifiers` in Task 10.

## File map

```
src/Sherlog.Core/
  Evidence/   CrashEvent.cs, EvidenceSources.cs, RawRecords.cs, IRecordSource.cs,
              ApplicationEventParser.cs, SystemEventParser.cs, ReliabilityRecordParser.cs,
              EventXml.cs (shared XML helpers), EvidenceReader.cs, LiveRecordSource.cs
  Grouping/   CrashGroup.cs, CrashGrouper.cs
  Recipes/    Recipe.cs (records), RecipeLoader.cs, RecipeLoadResult.cs
  Diagnosis/  ConfidenceLevel.cs, Diagnosis.cs, Diagnoser.cs, ConditionMatcher.cs, UnclearExplainer.cs
  Bundles/    Redactor.cs, RedactionTargets.cs, SystemSummary.cs, BundleWriter.cs, BundleReader.cs,
              BundleRecordSource.cs, LabelFile.cs, OwnerQuestions.cs
  Evaluation/ Evaluator.cs, EvalReport.cs
src/Sherlog.Cli/
  Program.cs, CliApp.cs (root command), CliEnvironment.cs (injectable deps),
  Commands/CrashesCommand.cs, ExplainCommand.cs, CollectCommand.cs, EvalCommand.cs, RecipesCommand.cs,
  Output/TextRenderer.cs, JsonOutput.cs
library/schema/recipe.schema.json, library/recipes/*.yaml
docs/recipe-format.md
tests/Sherlog.Core.Tests/  (mirrors src folders) + Fixtures/ (synthetic XML/JSON)
tests/Sherlog.Cli.Tests/
tests/seeded/  (C programs, expected.yaml, seeded recipes)
sandbox/       (.wsb configs for cases CI can't seed)
.github/workflows/ci.yml (seeded job added)
```

---

## Group A: Evidence (PR 1)

### Task 1: Windows target and evidence model

**Files:**
- Modify: `src/Sherlog.Core/Sherlog.Core.csproj`, `src/Sherlog.Cli/Sherlog.Cli.csproj`, both test `.csproj` (add `<TargetFramework>net10.0-windows</TargetFramework>`)
- Delete: `src/Sherlog.Core/Class1.cs`, `tests/*/UnitTest1.cs`
- Create: `src/Sherlog.Core/Evidence/CrashEvent.cs`, `EvidenceSources.cs`, `RawRecords.cs`, `IRecordSource.cs`
- Test: `tests/Sherlog.Core.Tests/Evidence/CrashEventTests.cs`

**Interfaces — Produces:**
```csharp
namespace Sherlog.Core.Evidence;
public static class EvidenceSources {           // recipe `source` values
  public const string ApplicationError = "application-error"; // Application 1000
  public const string ApplicationHang  = "application-hang";  // Application 1002
  public const string ErrorReport      = "wer";               // Application 1001 (non-APPCRASH)
  public const string DisplayReset     = "display-reset";     // System 4101
  public const string BugCheck         = "bugcheck";          // System 1001 (BugCheck)
  public const string UnexpectedShutdown = "kernel-power";    // System 41
  public const string Reliability      = "reliability";       // Win32_ReliabilityRecords
  public static IReadOnlyList<string> All { get; }
}
public sealed record CrashEvent(
  string Id, DateTimeOffset Time, string Source, string? Provider, int? EventId,
  string? AppName, string? AppVersion, string? FaultingModule, string? ModuleVersion,
  string? ExceptionCode, string? BugCheckCode, string? EventName, string? Message);
public sealed record RawEventRecord(string Log, string Xml, string? Message);   // Log: "Application" | "System"
public sealed record RawReliabilityRecord(IReadOnlyDictionary<string, string?> Properties);
public interface IRecordSource {
  IEnumerable<RawEventRecord> ReadEvents(DateTimeOffset since);
  IEnumerable<RawReliabilityRecord> ReadReliability(DateTimeOffset since);
  IReadOnlyList<string> Warnings { get; }       // filled while reading; never throws for an unreadable source
}
```
Normalization rules (all parsers): `ExceptionCode` and `BugCheckCode` are lowercase `0x` + 8 hex digits (`c0000005` → `0xc0000005`, `278` decimal BugcheckCode → `0x00000116`); `AppName`/`FaultingModule` keep the file name only (no path); `Time` is UTC.

- [ ] **Step 1:** Write `CrashEventTests.NormalizeCode_pads_and_prefixes` for a `public static string? CrashEvent.NormalizeCode(string? raw)` helper: `"c0000005"`→`"0xc0000005"`, `"0xC0000409"`→`"0xc0000409"`, `"278"` with `fromDecimal: true` → `"0x00000116"`, `""`/`null` → `null`. Signature: `NormalizeCode(string? raw, bool fromDecimal = false)`.
- [ ] **Step 2:** Run `dotnet test --filter CrashEventTests` → FAIL (type missing).
- [ ] **Step 3:** Retarget the four projects, delete template files, add the types above.
- [ ] **Step 4:** Run `dotnet test` → PASS; `dotnet build -c Release` → 0 warnings.
- [ ] **Step 5:** Commit `feat(core): evidence model and Windows target`.

### Task 2: Application log parser (1000, 1002, 1001)

**Files:** Create `src/Sherlog.Core/Evidence/EventXml.cs`, `ApplicationEventParser.cs`; fixtures `tests/Sherlog.Core.Tests/Fixtures/events/app-error-1000-named.xml`, `app-error-1000-positional.xml`, `app-hang-1002.xml`, `wer-1001-appcrash.xml`, `wer-1001-livekernelevent-141.xml`; test `Evidence/ApplicationEventParserTests.cs`.

**Interfaces:**
- Produces: `public static class ApplicationEventParser { public static CrashEvent? Parse(RawEventRecord record); }` returns `null` for any event that isn't one of the three. `EventXml` (internal): `Provider`, `EventId`, `RecordId`, `TimeUtc`, `Data(string name, int position)` (named first, positional fallback).
- Ids: `"app-" + EventRecordID`.

Before writing fixtures, confirm field layouts **without reading values**: on the dev PC, list only the `Name` attributes and count of `<Data>` elements for one recent event of each kind (e.g. PowerShell `Get-WinEvent -FilterHashtable @{LogName='Application';Id=1000} -MaxEvents 1 | % { ([xml]$_.ToXml()).Event.EventData.Data | % Name }`). Write fixtures from that shape with invented values (`SherlogTestGame.exe`, `FakeOverlay64.dll`, PC name `TESTPC`).

- [ ] **Step 1:** Tests: `AppError_named_data_maps_all_fields` (AppName `SherlogTestGame.exe`, AppVersion `1.2.3.4`, FaultingModule `FakeOverlay64.dll`, ModuleVersion `5.6.7.8`, ExceptionCode `0xc0000005`, Source `application-error`, EventId 1000, Id `app-<recordId>`); `AppError_positional_data_maps_same_fields`; `AppHang_maps_app_name_and_version` (Source `application-hang`, no module); `Wer_appcrash_is_ignored` (returns null: duplicate of 1000); `Wer_livekernelevent_maps_event_name_and_code` (Source `wer`, EventName `LiveKernelEvent`, BugCheckCode `0x00000141`); `Unrelated_event_returns_null`.
- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Implement with `System.Xml.Linq`, namespace `http://schemas.microsoft.com/win/2004/08/events/event`.
- [ ] **Step 4:** Run → PASS.
- [ ] **Step 5:** Commit `feat(core): parse application crash, hang and error-report events`.

### Task 3: System log and Reliability parsers

**Files:** Create `SystemEventParser.cs`, `ReliabilityRecordParser.cs`; fixtures `display-4101.xml` (Data[0] `nvlddmkm`, rendered message in German), `bugcheck-1001.xml` (param1 `0x00000116 (0x...)`), `kernel-power-41.xml` (BugcheckCode `0`), `reliability-app-failure.json`, `reliability-windows-failure.json`; tests `SystemEventParserTests.cs`, `ReliabilityRecordParserTests.cs`.

**Interfaces — Produces:** `SystemEventParser.Parse(RawEventRecord) : CrashEvent?` (ids `"sys-" + recordId`); `ReliabilityRecordParser.Parse(RawReliabilityRecord) : CrashEvent?` (ids `"rel-" + RecordNumber`; uses `SourceName`, `ProductName`, `EventIdentifier`, `TimeGenerated` (WMI datetime), `Message`; only records whose `EventIdentifier` is 1000, 1002 or 1001 or whose SourceName is `Windows Error Reporting`; everything else `null`).

- [ ] **Step 1:** Tests: `DisplayReset_driver_name_comes_from_data_not_message` (FaultingModule `nvlddmkm`, Source `display-reset`); `BugCheck_extracts_code_from_param1` (`0x00000116`); `KernelPower_zero_bugcheck_means_power_loss` (Source `kernel-power`, BugCheckCode `0x00000000`); `KernelPower_decimal_bugcheck_is_normalized` (`278` → `0x00000116`); `Reliability_app_failure_maps_product_and_time` (WMI time `20261120143000.000000-000` → 2026-11-20 14:30:00Z); `Reliability_unrelated_record_returns_null`.
- [ ] **Step 2:** Run → FAIL. **Step 3:** Implement. **Step 4:** Run → PASS.
- [ ] **Step 5:** Commit `feat(core): parse driver reset, blue screen, shutdown and reliability records`.

### Task 4: Evidence reader and live source

**Files:** Create `EvidenceReader.cs`, `LiveRecordSource.cs`; add packages `System.Diagnostics.EventLog` 10.0.15, `System.Management` 10.0.15 to Core; tests `EvidenceReaderTests.cs` (uses an in-memory `FakeRecordSource` in the test project), `LiveRecordSourceTests.cs`.

**Interfaces — Produces:**
```csharp
public sealed class EvidenceReader(IRecordSource source) {
  public IReadOnlyList<CrashEvent> ReadCrashes(DateTimeOffset since);   // newest first, de-duplicated
  public IReadOnlyList<string> Warnings { get; }
}
public sealed class LiveRecordSource : IRecordSource { }   // EventLogReader XPath per log; WMI Win32_ReliabilityRecords
```
De-duplication: a `reliability` event is dropped when an event-log crash with the same `AppName` (case-insensitive) exists within ±60 s. Live source XPath only asks for the providers/IDs in Tasks 2–3; it stores `EventRecord.ToXml()` and `FormatDescription()` (null on failure). Any `EventLogException`/`UnauthorizedAccessException`/`ManagementException` adds one warning (`"Couldn't read the System log, so blue screens and driver resets may be missing."` / similar for Application and Reliability) and continues.

- [ ] **Step 1:** Tests: `Reliability_duplicate_of_app_error_is_dropped`, `Reliability_without_event_log_twin_is_kept`, `Results_are_newest_first`, `Events_before_since_are_excluded`, `Unparseable_records_are_skipped`, `Source_warnings_are_passed_through`. `LiveRecordSourceTests.Reads_this_pc_without_throwing` (Windows only, asserts no exception and that the count is ≥ 0; **must not print or assert on any record content**).
- [ ] **Step 2:** Run → FAIL. **Step 3:** Implement. **Step 4:** Run → PASS.
- [ ] **Step 5:** Commit `feat(core): evidence reader with live Windows source`.

### Task 5: Crash grouping

**Files:** Create `src/Sherlog.Core/Grouping/CrashGroup.cs`, `CrashGrouper.cs`; test `Grouping/CrashGrouperTests.cs`.

**Interfaces — Produces:**
```csharp
public sealed record CrashGroup(string Id, string Signature, IReadOnlyList<CrashEvent> Crashes) {
  public CrashEvent Latest { get; }         // Crashes[0]; crashes are newest first
}
public static class CrashGrouper { public static IReadOnlyList<CrashGroup> Group(IEnumerable<CrashEvent> crashes); } // groups sorted by Latest.Time desc
```
Signature (lowercase, `|`-joined): app crashes/reliability → `app|{AppName}|{FaultingModule}|{ExceptionCode}`; hang → `hang|{AppName}`; display-reset → `display|{FaultingModule}`; bugcheck → `bugcheck|{BugCheckCode}`; kernel-power → `shutdown|{BugCheckCode}`; wer → `wer|{EventName}|{BugCheckCode}`. Missing fields render as empty. `Id` = `"g-"` + first 6 lowercase hex chars of SHA-256 of the signature.

- [ ] **Step 1:** Tests: `Same_app_module_code_form_one_group`, `Different_exception_code_splits_groups`, `Reliability_and_app_error_for_same_crash_share_a_group`, `Group_id_is_stable` (assert a literal expected id computed once by hand), `Groups_sorted_by_latest_crash`.
- [ ] **Steps 2–4:** FAIL → implement → PASS.
- [ ] **Step 5:** Commit `feat(core): group crashes by signature`. Open PR for Group A (`superpowers:requesting-code-review` first).

---

## Group B: Recipes and diagnosis (PR 2)

### Task 6: Recipe schema, format doc and loader

**Files:** Create `library/schema/recipe.schema.json` (JSON Schema 2020-12), `docs/recipe-format.md`, `src/Sherlog.Core/Recipes/Recipe.cs`, `RecipeLoader.cs`, `RecipeLoadResult.cs`; add YamlDotNet 18.1.0 and JsonSchema.Net 9.7.0 to Core; test `Recipes/RecipeLoaderTests.cs` with fixtures under `Fixtures/recipes/{valid,invalid}/`; modify `CLAUDE.md` "Validate recipes" line.

**Schema (Appendix B plus ADR 0004):** required `id` (`^[a-z0-9]+(-[a-z0-9]+)*$`), `version` (int ≥1), `title`, `category` (`crash|hang|driver-reset|bluescreen|shutdown`), `references` (≥1 https URI), `match.any` (≥1 condition; each has `source` from `EvidenceSources.All` and ≥1 more field from `provider, event_id, app_name, faulting_module, exception_code, bugcheck_code, event_name, message_contains`), `confidence.default` (`likely|unclear`), optional `confidence.confirmed_when[].occurrences_within_days {count ≥2, days ≥1}`, `diagnosis.summary`, optional `diagnosis.details`, optional `priority` (int, default 100), `fixes[]` (`id`, `risk` `safe|moderate|advanced`, `explain`, `actions[]` with `action` ∈ Appendix C names and optional `params` object, optional `when`, optional `verify`), `examples[]` (≥1; each an object of evidence fields with required `source`). `additionalProperties: false` everywhere.

**Interfaces — Produces:**
```csharp
namespace Sherlog.Core.Recipes;
public sealed record Recipe(string Id, int Version, string Title, string Category, IReadOnlyList<string> References,
  IReadOnlyList<MatchCondition> Match, ConfidenceRules Confidence, string Summary, string? Details,
  int Priority, IReadOnlyList<FixSuggestion> Fixes, IReadOnlyList<MatchCondition> Examples, string FilePath);
public sealed record MatchCondition(string Source, string? Provider, int? EventId, string? AppName, string? FaultingModule,
  string? ExceptionCode, string? BugCheckCode, string? EventName, string? MessageContains) {
  public int ConstrainedFieldCount { get; }      // non-null fields excluding Source
}
public sealed record ConfidenceRules(ConfidenceLevel Default, IReadOnlyList<RepeatRule> ConfirmedWhen);
public sealed record RepeatRule(int Count, int Days);
public sealed record FixSuggestion(string Id, string Risk, string Explain, IReadOnlyList<string> Actions);
public sealed record RecipeLoadResult(IReadOnlyList<Recipe> Recipes, IReadOnlyList<RecipeError> Errors);
public sealed record RecipeError(string FilePath, string Message);
public static class RecipeLoader {
  public static RecipeLoadResult LoadFolders(IEnumerable<string> folders, string schemaPath);
}
```
(`ConfidenceLevel` lives in `Sherlog.Core.Diagnosis`: `Unclear, Likely, Confirmed`.) Example entries reuse `MatchCondition` as evidence fields. Duplicate recipe IDs across files are an error on the second file.

- [ ] **Step 1:** Tests: `Valid_recipe_loads_all_fields` (Appendix B recipe plus an `examples` entry); `Unknown_action_is_rejected` (`action: run_script` → one error naming the file); `Confirmed_default_is_rejected`; `Missing_references_is_rejected`; `Bad_file_does_not_stop_other_files` (2 files, 1 bad → 1 recipe, 1 error); `Duplicate_id_is_an_error`.
- [ ] **Step 2:** Run → FAIL. **Step 3:** Implement (YAML → `JsonNode` via YamlDotNet's representation model, validate with JsonSchema.Net, then map). **Step 4:** Run → PASS.
- [ ] **Step 5:** Run `pipx run check-jsonschema --check-metaschema library/schema/recipe.schema.json` → ok. Write `docs/recipe-format.md` (every field, one full example, the specificity and confidence rules from ADR 0004).
- [ ] **Step 6:** Commit `feat(recipes): recipe schema v0, format doc and loader`.

### Task 7: Matching, confidence and the diagnoser

**Files:** Create `Diagnosis/ConfidenceLevel.cs`, `Diagnosis.cs`, `ConditionMatcher.cs`, `Diagnoser.cs`, `UnclearExplainer.cs`; tests `Diagnosis/ConditionMatcherTests.cs`, `DiagnoserTests.cs`, `LibraryExamplesTests.cs`; first real recipe `library/recipes/gpu-nvidia-driver-reset.yaml` (Appendix B + examples).

**Interfaces — Produces:**
```csharp
public static class ConditionMatcher { public static bool Matches(MatchCondition condition, CrashEvent crash); }
public sealed record Diagnosis(CrashGroup Group, Recipe? Recipe, ConfidenceLevel Confidence,
  string Summary, string? Details, IReadOnlyList<FixSuggestion> Fixes, string WhatWeKnow);
public sealed class Diagnoser(IReadOnlyList<Recipe> recipes) {
  public Diagnosis Diagnose(CrashGroup group);
  public IReadOnlyList<Diagnosis> DiagnoseAll(IEnumerable<CrashGroup> groups);
}
public static class UnclearExplainer { public static (string Summary, string Details) Explain(CrashGroup group); }
```
Rules: matching is against `group.Latest`; text compare `OrdinalIgnoreCase`; `*` wildcard in `AppName`/`FaultingModule` only; `MessageContains` is a case-insensitive substring of `Message`. Winner: highest `ConstrainedFieldCount` of its matching condition, then lowest `Priority`, then `Id` ordinal. Confirmed when any `RepeatRule` has ≥ `Count` crashes in the group within `Days` days ending at `Latest.Time` (never "now"). `WhatWeKnow` always lists app, failing file, error code and count in plain words (e.g. "SherlogTestGame.exe closed unexpectedly 4 times since Nov 3. Windows recorded the failure in FakeOverlay64.dll (error 0xc0000005)."). No recipe → `Confidence = Unclear`, summary/details from `UnclearExplainer`.

- [ ] **Step 1:** Tests: `Wildcard_module_matches` (`nv*.dll`), `All_fields_in_condition_must_hold`, `Message_contains_is_case_insensitive`; `Most_specific_recipe_wins`; `Priority_breaks_specificity_tie`; `Three_in_seven_days_is_confirmed` / `Three_in_ten_days_stays_likely`; `Window_ends_at_latest_crash_not_now`; `No_match_is_unclear_and_says_what_we_know`; `LibraryExamplesTests.Every_example_resolves_to_its_own_recipe` (loads `library/recipes` + schema from the repo root, fails listing every example that resolves elsewhere or to nothing).
- [ ] **Steps 2–4:** FAIL → implement → PASS.
- [ ] **Step 5:** Commit `feat(diagnosis): match recipes, assign confidence, explain unknown crashes`. Open PR for Group B.

---

## Group C: CLI, bundles and evaluation (PR 3)

### Task 8: CLI shell, `crashes`, `explain`, `recipes check`

**Files:** Modify `src/Sherlog.Cli/Program.cs`, `Sherlog.Cli.csproj` (AssemblyName `sherlog`; System.CommandLine 3.0.0; link `library/**` into output as `library/...`, `PreserveNewest`); create `CliApp.cs`, `CliEnvironment.cs`, `Commands/CrashesCommand.cs`, `ExplainCommand.cs`, `RecipesCommand.cs`, `Output/TextRenderer.cs`, `Output/JsonOutput.cs`; tests `tests/Sherlog.Cli.Tests/CrashesCommandTests.cs`, `ExplainCommandTests.cs`, `RecipesCommandTests.cs`.

**Interfaces — Produces:**
```csharp
public sealed record CliEnvironment(Func<string?, IRecordSource> OpenSource,   // null = live PC, else bundle path
  TextWriter Out, TextWriter Error, TimeProvider Clock, string LibraryRoot, Func<string?> ReadLine);
public static class CliApp {
  public static RootCommand Build(CliEnvironment env);
  public static int Run(string[] args, CliEnvironment env);
}
```
Commands and options:
- `sherlog crashes [--days <int>=30] [--bundle <path>] [--library <dir>...] [--json]` → one block per group: confidence word, one-line summary, count, last time (local time), group id.
- `sherlog explain (--latest | <id>) [--bundle] [--library] [--json]` → `<id>` is a group id (`g-…`) or crash id (`app-…`, `sys-…`, `rel-…`). Prints summary, confidence with a one-line meaning, details, "What Windows recorded" (`WhatWeKnow`), suggested fixes as a numbered list with risk, and a final line "Sherlog doesn't change anything on your PC in this version."
- `sherlog recipes check [--library <dir>...]` → lists errors, exit 1 if any; otherwise "N recipes OK", exit 0.
Exit codes: 0 success (including "no crashes"), 1 invalid input/unknown id/invalid recipes, 2 unexpected error. Invalid recipes during `crashes`/`explain` → one warning line on stderr per bad file, keep going. `--library` defaults to `<LibraryRoot>/recipes`; schema at `<LibraryRoot>/schema/recipe.schema.json`.

- [ ] **Step 1:** Tests (fake source + fixture recipes, `StringWriter` outputs): `Crashes_lists_groups_newest_first_with_confidence`; `Crashes_with_no_crashes_says_so_and_exits_0` (text `No crashes found in the last 30 days.`); `Crashes_json_is_valid_and_has_group_ids`; `Explain_latest_shows_summary_fixes_and_no_changes_line`; `Explain_unknown_id_exits_1_with_plain_message`; `Bad_recipe_warns_but_diagnosis_continues`; `Source_warning_is_printed_once`; `Recipes_check_fails_on_invalid_library`.
- [ ] **Steps 2–4:** FAIL → implement → PASS.
- [ ] **Step 5:** Manual check on the dev PC: `dotnet run --project src/Sherlog.Cli -- crashes --days 30` runs and exits 0. **Do not paste its output anywhere** (real data); only record the exit code and elapsed time.
- [ ] **Step 6:** Commit `feat(cli): crashes, explain and recipes check commands`.

### Task 9: Redactor

**Files:** Create `Bundles/RedactionTargets.cs`, `Redactor.cs`; test `Bundles/RedactorTests.cs`.

**Interfaces — Produces:**
```csharp
public sealed record RedactionTargets(string MachineName, string? DomainName, IReadOnlyList<string> UserNames) {
  public static RedactionTargets ForThisPc();     // Environment.MachineName, UserDomainName, UserName, folder names under the profiles directory (excluding Public, Default, Default User, All Users)
}
public sealed class Redactor(RedactionTargets targets) { public string Redact(string text); }
```
Order: profile paths (`(?i)\b([a-z]:\\users\\)[^\\/"<>\r\n]+` and the same with `/` and doubled `\\`) → `$1USER`; account SIDs `S-1-5-21-\d+(-\d+){2,4}` and `S-1-12-1-\d+(-\d+){3}` → `S-1-5-REDACTED`; machine name, domain name, user names as whole words, case-insensitive → `PC`, `DOMAIN`, `USER` (names shorter than 3 chars are only replaced inside paths). Well-known SIDs (`S-1-5-18`, etc.) stay.

- [ ] **Step 1:** Tests: `Profile_path_user_is_replaced` (also forward-slash and JSON-escaped forms); `Other_local_profiles_are_replaced`; `Machine_name_in_xml_Computer_element_is_replaced`; `Account_sid_is_replaced_system_sid_kept`; `Names_are_case_insensitive_whole_words` (`"Sam"` must not change `"Samsung"`); `Short_user_name_only_replaced_in_paths`.
- [ ] **Steps 2–4:** FAIL → implement → PASS. **Step 5:** Commit `feat(bundles): redactor`.

### Task 10: Evidence bundles, label file and `collect`

**Files:** Create `Bundles/SystemSummary.cs`, `BundleWriter.cs`, `BundleReader.cs`, `BundleRecordSource.cs`, `LabelFile.cs`, `OwnerQuestions.cs`; `src/Sherlog.Cli/Commands/CollectCommand.cs`; tests `Bundles/BundleRoundTripTests.cs`, `Bundles/LabelFileTests.cs`, `tests/Sherlog.Cli.Tests/CollectCommandTests.cs`.

**Bundle layout (zip):** `manifest.json` `{format:1, sherlogVersion, createdUtc, sinceUtc, redacted:true, eventCount, reliabilityCount}`; `system.json` (`SystemSummary`: Windows caption/version/build, CPU name, RAM GB rounded, GPUs `[{name, driverVersion, driverDate}]` from `Win32_VideoController`); `events.jsonl` (`{log, xml, message}` per line); `reliability.jsonl`; `label.yaml`.

**Interfaces — Produces:**
```csharp
public sealed record SystemSummary(string Windows, string Build, string Cpu, int MemoryGb, IReadOnlyList<GpuInfo> Gpus) { public static SystemSummary ForThisPc(); }
public sealed record GpuInfo(string Name, string? DriverVersion, string? DriverDate);
public static class BundleWriter { public static void Write(string zipPath, IRecordSource source, DateTimeOffset since,
  SystemSummary system, LabelFile label, Redactor redactor, string sherlogVersion); }
public sealed class BundleReader : IDisposable { public static BundleReader Open(string zipPath);
  public BundleManifest Manifest { get; } public SystemSummary System { get; } public LabelFile Label { get; }
  public IRecordSource Records { get; } }                // BundleRecordSource over the jsonl files
public sealed record LabelFile(OwnerAnswers Owner, InvestigatorSection Investigator) {
  public static LabelFile Parse(string yaml); public string ToYaml(); }
public sealed record OwnerAnswers(string? WhatCrashed, string? Doing, IReadOnlyList<string> RecentChanges,
  IReadOnlyList<string> Tried, string? Helped, string? StillHappens, string? Note);
public sealed record InvestigatorSection(bool Seeded, IReadOnlyList<CrashLabel> Labels);
public sealed record CrashLabel(string? GroupId, string? AppName, string TrueCause, string? Evidence, string LabelStrength);
public static class OwnerQuestions { public static OwnerAnswers Ask(TextWriter output, Func<string?> readLine, IReadOnlyList<CrashGroup> groups); }
```
`label.yaml` uses snake_case keys (`what_crashed`, `true_cause`, `label_strength`, …) and keeps every question as a YAML comment above its answer, so an empty file can be filled in by hand. `collect` pre-fills one `CrashLabel` per crash group (`true_cause: unknown`, `label_strength: unknown`). `LabelStrength` ∈ `seeded|confirmed-by-fix|owner-reported|unknown`; `TrueCause` is a recipe id or `unknown`.

Owner questions (numbers to pick; several allowed where marked): 1 What crashed? (numbered list of the 9 most recent crash groups by app/driver name, plus "Something else"); 2 What were you doing? (Playing a game / Watching a video / Working or browsing / The PC was idle / Starting up or shutting down / I don't remember); 3 Did anything change before it started? *(several)* (New graphics driver / Windows update / New app or game / Overclock, BIOS or XMP change / New hardware / Nothing I know of / Not sure); 4 What have you tried? *(several)* (Updated a driver / Rolled back a driver / Reinstalled or verified the game / Turned off an overlay / Removed an overclock / Reinstalled Windows / Nothing yet); 5 Did that fix it? (Yes / Partly / No / I haven't tried anything); 6 Does it still happen? (Yes, often / Sometimes / Not since the fix / Not sure); 7 Anything else? (free text, Enter to skip). Blank answer = skip. Final wording comes from Task 14.

`sherlog collect [--redact] [--out <path>=sherlog-evidence.zip] [--days <int>=90] [--no-questions]` → reads live source, asks questions unless `--no-questions`, writes bundle, prints counts, the full path and "Nothing in this file names you or your PC. You can open it to check before sending it."

- [ ] **Step 1:** Tests: `Bundle_round_trip_preserves_records_and_manifest`; `Bundle_contains_no_original_identifiers` (fake source whose XML/messages/reliability rows contain `TESTUSER`, `TESTPC`, `C:\Users\TESTUSER\Games\x.exe` and an `S-1-5-21-…` SID; unzip every entry and assert none of those strings survive, case-insensitive); `Bundle_reader_source_diagnoses_like_live` (same groups from a bundle as from the fake source); `Label_round_trip_and_comments_present`; `Label_parses_hand_filled_file`; `Owner_questions_record_multi_choice_and_skips`; `Collect_no_questions_writes_prefilled_labels`.
- [ ] **Steps 2–4:** FAIL → implement → PASS.
- [ ] **Step 5:** Manual: `dotnet run --project src/Sherlog.Cli -- collect --no-questions --out %TEMP%\sherlog-selftest.zip`; then search the zip contents for this PC's user name and machine name with a script that prints **only** "found"/"not found" per term. Expected: not found. Delete the zip.
- [ ] **Step 6:** Commit `feat(bundles): redacted evidence bundles, label file and collect command`.

### Task 11: `eval`

**Files:** Create `src/Sherlog.Core/Evaluation/Evaluator.cs`, `EvalReport.cs`, `src/Sherlog.Cli/Commands/EvalCommand.cs`; tests `Evaluation/EvaluatorTests.cs`, `tests/Sherlog.Cli.Tests/EvalCommandTests.cs`.

**Interfaces — Produces:**
```csharp
public sealed record EvalReport(SeededScore Seeded, FieldScore Field, IReadOnlyList<EvalMiss> Misses, int BundleCount);
public sealed record SeededScore(int Labeled, int Correct);                     // Correct = matched recipe == TrueCause
public sealed record FieldScore(IReadOnlyDictionary<string, StrengthScore> ByStrength, int WrongConfirmed,
  int Groups, int UnclearGroups, int QualifyingBundles);                        // QualifyingBundles: ≥1 confirmed-by-fix/owner-reported label
public sealed record StrengthScore(int Labeled, int ConfidentDiagnoses, int ConfidentCorrect);   // Confident = Confirmed or Likely
public sealed record EvalMiss(string Bundle, string CrashGroup, string Expected, string? Got, ConfidenceLevel Confidence);
public static class Evaluator { public static EvalReport Evaluate(IEnumerable<string> bundlePaths, IReadOnlyList<Recipe> recipes); }
```
A label with `AppName` instead of `GroupId` (YAML `crash_group`) applies to every group whose `Latest.AppName` matches. `unknown` true causes count in `Groups`/`UnclearGroups` only. `sherlog eval <folder> [--library <dir>...] [--json]` evaluates every `*.zip` in the folder (non-recursive), prints seeded `Correct/Labeled`, field precision per strength, wrong Confirmed, Unclear share, qualifying bundles, and the list of misses; exits 0 even when the gate fails (the gate is judged in `docs/gates/phase-1.md`).

- [ ] **Step 1:** Tests: `Seeded_scores_correct_matches`; `Field_precision_split_by_strength`; `Unknown_labels_only_count_toward_coverage`; `Wrong_confirmed_is_counted`; `App_name_label_resolves_to_groups`; `Qualifying_bundles_counted`; CLI `Eval_prints_summary_and_misses`.
- [ ] **Steps 2–4:** FAIL → implement → PASS.
- [ ] **Step 5:** Commit `feat(eval): score diagnoses against labeled bundles`. Open PR for Group C.

---

## Group D: Test data and recipes (PR 4, may split)

### Task 12: Seeded crashes on CI (and sandbox fallbacks)

**Files:** Create `tests/seeded/src/seed_module.c` (DLL `SherlogSeedModule.dll`, export `seed_crash()` writing through a null pointer), `seed_crash_in_module.c` (loads the DLL, calls it), `seed_stack_overflow.c` (`SherlogSeedOverflow.exe`, unbounded recursion → `0xc00000fd`), `seed_fastfail.c` (`SherlogSeedFastFail.exe`, `__fastfail(7)` → `0xc0000409`), `tests/seeded/build.cmd`, `tests/seeded/recipes/*.yaml` (one test recipe per program, with examples), `tests/seeded/expected.yaml` (a `LabelFile` investigator section with `seeded: true` and one `app_name` label per program); modify `.github/workflows/ci.yml` (new job `seeded`, `needs: build`); create `sandbox/seeded-missing-runtime.wsb`, `sandbox/README.md`.

Job `seeded` steps: set up MSVC (`ilammy/msvc-dev-cmd@v1`), `tests/seeded/build.cmd`, run each program (ignore its non-zero exit), wait 10 s for Windows Error Reporting, `dotnet run --project src/Sherlog.Cli -c Release -- collect --no-questions --days 1 --out seeded/seeded.zip`, replace the bundle's `label.yaml` with `tests/seeded/expected.yaml` (a small `sherlog`-free PowerShell zip update), then `sherlog eval seeded --library library/recipes --library tests/seeded/recipes --json` and fail the job unless `Seeded.Correct == Seeded.Labeled` and `Labeled == 3`. Upload the bundle as an artifact (runner data only).

- [ ] **Step 1:** Push the job and read its result. If a runner doesn't log Application Error 1000 for these programs, follow `superpowers:systematic-debugging`; if the cause is the runner's WER setup, record the gap in `sandbox/README.md` and move that case to a `.wsb` config (ADR 0007).
- [ ] **Step 2:** `sandbox/seeded-missing-runtime.wsb`: maps a folder holding a CI-built `SherlogSeedNeedsRuntime.exe` (linked against the dynamic VC runtime) plus a published `sherlog.exe`; logon command runs the program, then `sherlog collect --no-questions --out C:\seeded\out\missing-runtime.zip`. First record which event (if any) Windows writes when a runtime DLL is missing; the recipe for missing runtimes depends on it.
- [ ] **Step 3:** Commit `test(seeded): crash known programs on CI and score them`.

### Task 13: Seed recipes (15, then 50)

**Files:** `library/recipes/*.yaml`; test coverage comes from `LibraryExamplesTests` (Task 7) and CI schema validation.

Research with `superpowers:dispatching-parallel-agents`, one agent per area: graphics drivers (NVIDIA/AMD/Intel resets and TDR codes 0x116/0x117/0x141), overlays and injectors (Discord, Steam, RivaTuner/RTSS, MSI Afterburner, OBS hooks, GeForce Experience), game files (crashes inside the game's own modules, launcher verification), memory and overclock (random bug checks 0x1A, 0x50, 0x3B, 0x124 WHEA), missing runtimes (VC++ `vcruntime140*.dll`/`msvcp140*.dll`, DirectX `d3dx9_*.dll`/`xinput1_3.dll`, .NET), antivirus and anti-cheat interference, power and PSU (Kernel-Power 41 with bug check 0), storage (0x7A, 0xF4). Each agent returns draft recipes with sources; the main session reviews every one, writes `examples`, and runs `dotnet test` + `sherlog recipes check`. Only sources that are vendor docs, Microsoft Learn, or widely cited issue trackers count.

- [ ] **Step 1:** 15 recipes, all tests green → commit `feat(library): first 15 crash recipes`.
- [ ] **Step 2:** Grow to 50 → commit `feat(library): 50 crash recipes`.

### Task 14: Wording pass

Use `design:ux-copy` on every recipe `summary`/`details`/fix `explain`, `UnclearExplainer` text, confidence lines, owner questions, `collect` messages and CLI errors. Update tests that pin exact text. Commit `docs(library): plain-language wording pass`. Open PR for Group D.

---

## Group E: Field test and gate (PR 5)

### Task 15: Field-test kit

**Files:** Create `docs/field-test.md` (for the owner: how to send `sherlog.exe` to friends, what friends do, how bundles come back, privacy promise in plain words), `docs/field-test-friend.md` (one page for friends); modify `ci.yml` to upload a self-contained single-file `sherlog.exe` artifact (`dotnet publish src/Sherlog.Cli -c Release -r win-x64 --self-contained -p:PublishSingleFile=true`).

- [ ] **Step 1:** Measure `sherlog explain --latest` on the dev PC 5 times (`Measure-Command`), record median (Gate: < 5 s). Record only timings.
- [ ] **Step 2:** Commit `docs: field-test kit` and ask the owner (👤) to start the field test. The build is unsigned; the friend guide says so and explains the SmartScreen prompt.

### Task 16: Investigation and gate (👤 owner-paced)

- [ ] For each returned bundle: read **only** `sherlog crashes --bundle <zip> --json`, `system.json` and the owner answers (never raw records in chat), fill the investigator labels with evidence, and keep bundles out of the repo (they stay in a local folder outside the repo).
- [ ] Where a fix was tried, ask the owner for a follow-up a week later; upgrade labels to `confirmed-by-fix` when the crash stopped for ≥ 7 days.
- [ ] Run `sherlog eval <folder>`; write `docs/gates/phase-1.md` with seeded score, field precision by strength, wrong Confirmed, Unclear share, qualifying bundles, explain timing and every miss. If any Gate 1 line fails, improve parsers/recipes and repeat. Do not start Phase 2.
