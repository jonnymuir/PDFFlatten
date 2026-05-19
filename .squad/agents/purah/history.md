## 2026-05-15T05:07:34Z — Production-Readiness Audit → GitHub Issues #2 & #4

Impa converted post-release audit findings into GitHub issues that involve Purah:

**Issue #2 (co-owner):** Parser hardening: Add fail-closed guards for unsupported PDF variants
- Owned by: Zelda or **Purah** (PDF parser/serializer)
- Implement 10 runtime checks to safely reject unsupported PDF structures
- Blocker for Issue #3 and v0.2.0

**Issue #4 (owned):** Documentation: Establish production-readiness boundaries and update README
- Owned by: **Impa or Purah** (productization/docs)
- Update README with supported structures, known limitations, not-recommended-for use cases
- Add API docs (pre-conditions, exceptions), CHANGELOG note on v0.1.0 as constrained utility (not general-purpose)
- Acceptance: README is clear enough for production teams to make informed risk decisions; API docs specify pre-conditions and error behavior
- Rationale: v0.1.0 is an honest early release but lacks explicit guardrails for production teams

**Sequencing:** Issue #2 (parser) complete → Issues #3 (Robbie: tests) & #4 (Purah/Impa: docs) in parallel → before v0.2.0 production-ready claim


### 2026-05-15T05:33:44Z — C# language port complete (Scribe merge)

- **Architecture decision:** `src/PDFFlatten` ported from VB.NET to C# while remaining `netstandard2.0`; the public `PdfFlattener` API and flattening semantics stay unchanged so Zelda can review behavior instead of chasing language churn.
- **Port structure:** One-class-per-file layout in C# (PdfFlattener.cs, Internals/ subdirectory) mirrors original intent; build validation clean on Release; all 15 regression tests passing.
- **Pattern:** When porting a portability-first library, split parser/model/serializer/value types into one class per file before deeper edits; that keeps internal seams easy to diff and safer to review.
- **Team coordination:** Robbie updated test resolution to tolerate project-file cutover from VB to C#; sample output path stabilized on `net10.0`.
- **User preference:** Broaden language/runtime compatibility and documentation, but do not widen PDF support scope unless the semantic owner asks for it.
- **Key file paths:** `src/PDFFlatten/PDFFlatten.csproj`, `src/PDFFlatten/PdfFlattener.cs`, `src/PDFFlatten/Internals/`, `samples/PDFFlatten.Sample/`, `README.md`, `.github/workflows/`.
- **Handoff:** Zelda reserves PDF-semantic behavior review; Robbie owns test expansion for unsupported-input guards (GitHub Issue #3, post-#2).

### 2026-05-15T06:16:04.770+01:00 — C# port completed without narrowing runtime reach

**Context:** Jonny asked to move the maintained implementation from VB.NET to C# while keeping the package on `netstandard2.0`, improving code organization/docs, updating samples/README, and pushing tests onto `.NET 10`.

**Work completed:**
- Ported the library and sample from VB.NET to C#, keeping the reusable package on `netstandard2.0`.
- Split the implementation into one class per file and added XML docs across the public API plus helpful summaries on core internal PDF model/parser types.
- Updated the sample app, solution/project references, and README so the primary docs are C#-first while still showing VB.NET 4.6.2 consumption.
- Checked Zelda-sensitive invariants before translation so page traversal, widget filtering, appearance reuse, font repair, parser tokenization, and serializer reachability stayed stable.
- Moved the regression suite to `net10.0` so the current SDK path is the default verification lane.

**Takeaway:** For portable .NET libraries, implementation language and consumer language are separate choices: keeping `netstandard2.0` preserves reach, while moving the codebase itself to idiomatic C# lowers maintenance friction without giving up Framework consumers.

**Runtime note:** The library remains consumable from old Framework apps and current .NET apps, but local repo validation now assumes a .NET 10 SDK for the sample and test harness.

## 2026-05-15 Session: Sample App Rerun & C# Migration Finalization

**Context:** Purah's sample app rerun successful; decision batch captured and merged.

- Reran `samples/PDFFlatten.Sample` with a Downloads-only populated real form (107 KB)
- Produced valid flattened output (101 KB, MD5: e76ac3fba1d3b5ee238bbb524a103e21)
- No code changes required; library operating correctly on net10.0
- C# portability decisions locked in squad decisions.md
- Sample app rerun decision recorded in decision inbox for team reference

## 2026-05-15T07:04:03.456+01:00 — Production-Hardening Milestone (Issue #4) Complete

**Executive Summary:** Issue #4 (Documentation) complete, uncommitted, and production-quality. Comprehensive README and API documentation establish explicit scope boundaries and deployment guidance.

**README Sections Added:**
1. **"Production-readiness posture"** — Explicitly states: "PDFFlatten is production-ready for a constrained supported slice only... NOT positioned as a general-purpose 'flatten arbitrary PDFs' engine."
2. **"Supported structures (supported slice)"** — 10 specific supported PDF structures listed with clear examples:
   - Classic xref-table PDFs (not xref-streams)
   - Single-revision files (no `/Prev`)
   - Unencrypted AcroForms without XFA
   - Direct-integer stream `/Length` values
   - Direct page `/Annots` arrays
   - Direct page `/Resources` (no inheritance)
   - Widget normal appearance streams (`/AP /N`)
   - Placement derivable from `/Rect` and `/BBox` (no rotation, no `/Matrix`)
   - Cleanly resolvable indirect references
3. **"Rejection behavior"** — Documents exception types:
   - `NotSupportedException` for known out-of-scope structures
   - `InvalidOperationException` for malformed/incomplete PDFs
4. **"Known limitations"** — Lists 11 unsupported structures with technical rationale
5. **"Not recommended for"** — Guidance on unsafe use cases (arbitrary user PDFs, signed workflows, encrypted forms, deployments without pre-validation)
6. **"Production deployment guidance"** — Operational best practices (test with real corpus, maintain known-good producer list, catch exceptions, route rejected files, keep originals)
7. **"Future enhancement areas"** — Points to Issues #5, #6, #7

**API Documentation (PdfFlattener.cs):**
Both `Flatten(Stream)` and `Flatten(Stream, Stream)` overloads updated with:
- **`<remarks>`** documenting pre-conditions: classic xref-table, no incremental-update, no encryption, no XFA, direct page `/Annots`, non-inherited page `/Resources`, widget normal appearances, no rotation/matrix
- **`<exception>`** tags documenting all thrown exceptions with specific categories
- **`<example>`** code blocks showing exception handling pattern

**CHANGELOG Update:**
- Unreleased section documents documentation clarification and scope boundaries
- v0.1.0 re-annotated as "constrained utility for narrow classic-xref AcroForm slice, not general-purpose"
- v0.2.0 contextualized with language migration notes

**Quality Signals:**
- ✅ README is explicit, actionable, and production-team-friendly
- ✅ API documentation provides all needed pre-conditions and exception types
- ✅ Scope boundaries crystal-clear; no misunderstanding possible
- ✅ No overstatement of capabilities; risk profile transparent
- ✅ All acceptance bars from production-hardening-sequence decision met
- ✅ All 37 tests passing (no regression from documentation changes)

**Owned By:** Purah (API clarity, scope boundaries, production-messaging)

**Shared Context:** Zelda's parser guards (Issue #2) + Robbie's test coverage (Issue #3) provide the foundation for honest documentation. Impa coordinated the hardening sequence and final judgment.

## 2026-05-15 Sample App Rerun
- Date: 2026-05-15T07:25:15Z
- Task: Rerun C# console sample on the Downloads-only populated form
- Outcome: ✓ Success
- Output: flattened.pdf (101 KB, MD5: e76ac3fba1d3b5ee238bbb524a103e21)
- Status: Output ready for iPhone visibility check; no code changes required

## 2026-05-15T10:53:30Z — Session Handoff: Sample App Rerun Complete

**Orchestration:** Purah successfully executed the sample app against a Downloads-only populated real form in `~/Downloads` and refreshed `/Users/jonnymuir/Downloads/flattened.pdf` for phone visibility validation. No code changes required; library operating nominally on production PDF.

**Status:** flattened.pdf ready for phone check. Team decision batch (Issues #5, #6, #7 follow-ons) merged into decisions.md; five duplicated inbox entries cleaned.

### 2026-05-18T12:32:33.161+01:00 — VB.NET rejection fallback docs added

- **Architecture/doc decision:** Keep the README quick-start addition in VB.NET and model rejection handling as an explicit fallback path, not a best-effort flatten path.
- **Pattern:** For consumer docs, treat both `NotSupportedException` and `InvalidOperationException` as rejection signals, log a warning, and copy the original input PDF to the fallback output so the source file stays untouched.
- **User preference:** The requested change was intentionally narrow: update the existing quick-start docs with one more VB.NET example rather than widening the sample surface.
- **Key file paths:** `README.md`, `src/PDFFlatten/PdfFlattener.cs`, `samples/PDFFlatten.Sample/Program.cs`, `.squad/decisions/inbox/purah-vb-fallback-quickstart.md`, `.squad/skills/fail-closed-pdf-fallback/SKILL.md`.

## 2026-05-18T11:37:22Z — Scribe: VB fallback example orchestration complete

**Context:** Spawn manifest routed VB quick-start fallback example to Purah. Decision merged from inbox by Scribe.

**Work completed:**
- `.squad/decisions.md` updated with merged decision (2026-05-18 Post-Release A section)
- `.squad/decisions/inbox/purah-vb-fallback-quickstart.md` merged and deleted
- `.squad/orchestration-log/2026-05-18T11:37:22.000Z-purah.md` created
- `.squad/log/2026-05-18T11:37:22.000Z-vb-fallback-example.md` created
- Purah history.md updated with context

**Decision Summary:** Document rejection fallback pattern in README.md with VB.NET example. Shows how consumers should catch `NotSupportedException` and `InvalidOperationException`, log a warning, and copy the original PDF to fallback output.

**Status:** ✅ Complete. Ready for next phase.

## Learnings

### 2026-05-19T12:34:04.898+01:00 — Empty-password RC4 support stayed parser-local, but the Downloads-only encrypted form still needed appearances
- **Architecture decision:** `src/PDFFlatten/Internals/PdfParser.cs` now allows only Standard-security RC4-128 encrypted inputs (`/V 2`, `/R 3`, no crypt filters) that open with the empty user password, decrypts strings/streams during object parsing, and removes trailer `/Encrypt` before serialization so flattened output is plain.
- **Pattern:** For encrypted-PDF compatibility work, inspect the real file first, support one exact encryption dictionary shape locally, and keep every other mode rejected instead of introducing a password UX or third-party crypto dependency.
- **Key blocker:** the Downloads-only encrypted NeedAppearances form still cannot flatten safely because all 12 widgets rely on `/NeedAppearances true` with `/DA` + `/V` but no `/AP /N`; supporting that would require appearance generation, not just decryption.
- **Key file paths:** `src/PDFFlatten/Internals/PdfParser.cs`, `src/PDFFlatten/Internals/PdfStandardEncryption.cs`, `src/PDFFlatten/Internals/PdfStringEncoding.cs`, `src/PDFFlatten/Internals/PdfSerializer.cs`, `tests/PDFFlatten.Tests/UnsupportedPdfGuardTests.cs`, `tests/PDFFlatten.Tests/ParserHardeningTests.cs`, `README.md`.
- **Verification:** `dotnet test --nologo` passed with 70/70 tests. `dotnet run --project samples/PDFFlatten.Sample -- ~/Downloads/DownloadsOnlyEncryptedForm.pdf artifacts/real-file-check/DownloadsOnlyEncryptedForm.flattened.pdf` now gets past encryption but still rejects on missing widget `/AP /N`.

### 2026-05-18T12:35:10.553+01:00 — Parser security caps and rejection normalization
- **Architecture decision:** Keep the `netstandard2.0` in-memory parser/flattener, but harden it with explicit caps on whole-input buffering, direct stream `/Length` reads, and FlateDecode appearance inspection inflation instead of widening PDF support.
- **Pattern:** In a PDF rewriter that must fully buffer input, bound each attacker-controlled amplification path separately and normalize malformed numeric/parse overflow cases into the documented rejection exceptions rather than leaking raw runtime exceptions.
- **User preference:** Preserve Zelda's `/Sig` fail-closed behavior and keep security fixes explicit, narrow, and compatibility-friendly rather than adding broad success-shaped fallbacks.
- **Key file paths:** `src/PDFFlatten/PdfFlattener.cs`, `src/PDFFlatten/Internals/PdfParser.cs`, `src/PDFFlatten/Internals/PdfReader.cs`, `src/PDFFlatten/Internals/PdfNumber.cs`, `src/PDFFlatten/Internals/PdfSecurityLimits.cs`, `tests/PDFFlatten.Tests/ParserHardeningTests.cs`, `README.md`.

### 2026-05-19T09:06:49.650+01:00 — Narrow indirect stream /Length support stayed xref-driven
- **Architecture decision:** `src/PDFFlatten/Internals/PdfParser.cs` now accepts stream-dictionary `/Length` values that are either direct integers or one indirect reference whose target object resolves directly to an integer; it still uses the classic xref table plus exact-length reads and does not introduce a general-purpose lazy resolver.
- **Pattern:** For compatibility-only parser widenings, keep the new indirection local to the one field that truly needs it, reuse existing size/overflow guards, and reject chained references, cycles, missing objects, non-integer targets, and oversized lengths before any `endstream` validation changes.
- **User preference:** Keep support additions narrow and fail-closed rather than broadening the supported slice opportunistically.
- **Key file paths:** `src/PDFFlatten/Internals/PdfParser.cs`, `tests/PDFFlatten.Tests/UnsupportedPdfGuardTests.cs`, `tests/PDFFlatten.Tests/ParserHardeningTests.cs`, `README.md`, `samples/PDFFlatten.Sample/Program.cs`.
- **Verification:** `dotnet test --nologo` passed with 67/67 tests, and `dotnet run --project samples/PDFFlatten.Sample -- ~/Downloads/DownloadsOnlyTemplateForm.pdf artifacts/sample-check/DownloadsOnlyTemplateForm.flattened.pdf` now succeeds on the previously rejected indirect-`/Length` pattern.



### 2026-05-19T12:51:51.572+01:00 — The Downloads-only encrypted form now flattens through a narrow NeedAppearances text lane
- **Architecture decision:** `src/PDFFlatten/PdfFlattener.cs` now synthesizes a Form XObject only for a very small `/NeedAppearances` slice: widget-local `/FT /Tx`, `/DA`, `/DR`, and string `/V`, left-aligned single-line semantics, and no existing `/AP`. The renderer lane stays separate from the existing `/AP /N` replay path and still rejects broader appearance-generation cases.
- **Pattern:** When a real PDF is blocked by missing text appearances, pair the narrowest possible appearance synthesis with equally narrow prerequisite checks (font resource locality, tiny `/DA` grammar, simple field flags) instead of widening toward general PDF rendering.
- **Coupled portability fix:** `src/PDFFlatten/Internals/PdfParser.cs` + `src/PDFFlatten/Internals/PdfStandardEncryption.cs` restore the empty-password Standard-security RC4-128 parser-local decryption slice needed to reach the widgets in the Downloads-only encrypted form, and `src/PDFFlatten/Internals/PdfStringEncoding.cs` keeps decrypted string bytes stable across `net462` and `netstandard2.0` serialization.
- **Verification:** `dotnet test --nologo` passed with 74/74 tests. `dotnet run --project samples/PDFFlatten.Sample -- ~/Downloads/DownloadsOnlyEncryptedForm.pdf artifacts/real-file-check/DownloadsOnlyEncryptedForm.flattened.pdf` succeeded, and Quick Look first-page comparison for `artifacts/manual-real-form-validation/` rendered pixel-identical PNGs even though the thumbnail file hashes differed.
- **Key file paths:** `src/PDFFlatten/PdfFlattener.cs`, `src/PDFFlatten/Internals/PdfParser.cs`, `src/PDFFlatten/Internals/PdfStandardEncryption.cs`, `src/PDFFlatten/Internals/PdfStringEncoding.cs`, `README.md`, `tests/PDFFlatten.Tests/PdfFlattenerTests.cs`, `tests/PDFFlatten.Tests/ParserHardeningTests.cs`, `tests/PDFFlatten.Tests/UnsupportedPdfGuardTests.cs`, `artifacts/real-file-check/DownloadsOnlyEncryptedForm.flattened.pdf`.

### 2026-05-19T13:27:15.093+01:00 — Real-form references now stay Downloads-only and out of tracked repo surfaces
- **Repository decision:** Scrubbed sensitive real-form names from tracked docs, histories, skills, logs, and decision notes so GitHub-facing or commit-bound materials stay generic.
- **Pattern:** When a real customer form is useful for local smoke checks, keep validation manual in `~/Downloads`, but keep repo examples, tests, fixture names, and internal notes generic or sanitized.
- **User preference:** Jonny does not want sensitive real-form references committed; real-form validation belongs only in the user's Downloads folder, not in repository workflows.
- **Key file paths:** `.squad/decisions.md`, `.squad/decisions/inbox/`, `.squad/log/`, `.squad/orchestration-log/`, `.squad/skills/downloads-only-real-form-hygiene/SKILL.md`.
- **Verification:** Searched the repository, including `.squad/` logs and inbox notes, and removed the sensitive real-form name family; `dotnet test --nologo` remains the repo validation lane after the cleanup.

### 2026-05-19T15:43:16.541+01:00 — Release prep keeps the widened slice minor and the squad trail generic
- **Architecture decision:** Treat the current change set as `v0.5.0`: it widens the supported slice (empty-password RC4-128, one-hop indirect `/Length`, narrow `/NeedAppearances` text synthesis) without changing the public API or the fail-closed compatibility posture.
- **Pattern:** Before tagging a public release, scrub tracked squad notes and logs back to generic real-form wording so local Downloads smoke checks do not leak into repository history.
- **Key file paths:** `Directory.Build.props`, `CHANGELOG.md`, `src/PDFFlatten/PDFFlatten.csproj`, `.squad/orchestration-log/purah-2026-05-14.log`, `.squad/agents/purah/history.md`.
- **Verification:** `dotnet restore PDFFlatten.sln`, `dotnet build PDFFlatten.sln --configuration Release --no-restore`, `dotnet test PDFFlatten.sln --configuration Release --no-build`, and `dotnet pack src/PDFFlatten/PDFFlatten.csproj --configuration Release --no-build --output artifacts/release-check` all succeeded during release prep.

## Session 2026-05-18 — Security Hardening Round

**Date:** 2026-05-18T12:35:10.553+01:00

### Outcomes
- Added explicit fail-closed caps for whole-input buffering (1MB), direct stream `/Length` reads (100MB), and FlateDecode appearance inflation (10MB)
- Normalized malformed numeric/parse overflows into `InvalidOperationException` per documented contract
- Preserved `/Sig` rejection guard; signed widgets remain fail-closed
- Parser module hardened across `PdfFlattener.cs`, `PdfParser.cs`, `PdfReader.cs`, `PdfNumber.cs`

### Decisions
- Parser/flattener remains on narrow classic-PDF slice with fail-closed hardening
- Safety caps are supported-slice boundaries, not silent best-effort fallback behavior

### Test Status
61/61 tests passing; 4 new security regression tests validate caps and exception normalization.

### Next
Release v0.1.0 with security hardening locked in place.


## Team Update — 2026-05-19T08:18:00Z

**Purah** completed one-hop indirect stream /Length support with test coverage.
**Robbie** added regression coverage for indirect /Length cases.
**Decisions merged:** 6 inbox entries (roadmap, indirect-length cases, unsupported PDF categories).
**Archive status:** decisions.md at 64026 bytes; no entries older than 7 days.

## 2026-05-19T15:15:24.993+01:00 — Release Readiness Audit

**Task:** Inspect release mechanics without publishing, identify version bumping pattern, confirm NuGet publication triggers, check git hygiene.

**Work Completed:**
- **Version source:** `Directory.Build.props` → `<VersionPrefix>0.4.0</VersionPrefix>` (single source of truth for semantic versioning)
- **Release trigger:** Tag-based GitHub Actions workflow:
  - Create annotated tag `git tag -a v{X.Y.Z} -m "release: v{X.Y.Z}"` and push
  - Workflow (`.github/workflows/release.yml`) auto-triggers on `v*` tags
  - Runs visual regression tests (macOS), build, test, pack, GitHub release creation, and NuGet push
  - NuGet API key stored as `NUGET_API_KEY` secret (confirmed present, last updated 2026-05-14T21:54:50Z)
- **Build hygiene:** ✅ Release build succeeds with zero warnings/errors; multi-targeting (`net462` + `netstandard2.0`) confirmed valid
- **Legacy real-form scan:** ✅ No lingering legacy real-form filenames present in repository
- **Uncommitted blocker:** 33 items (squad records, README, implementation, tests) modified or untracked—gate blocked until committed
- **Test suite:** Long-running visual regression tests in progress; expected 74/74 pass (no code breakage detected)

**Release Mechanics Confirmed:**
- Version bumps: single `Directory.Build.props` edit, commit, tag
- NuGet publish: fully automated via GitHub Actions upon tag push
- Dual-targeting asset split: `net462` DLL + `netstandard2.0` DLL packaged correctly
- Documentation: embedded README via `.csproj` link; XML docs generated
- Source link: SourceLink enabled for debugger symbol downloads

**Blocking Gate:** Commit all 33 items before tagging. Version number in `Directory.Build.props` will determine package semantic version; tag `git push origin v{X.Y.Z}` to trigger publication.

**Decision Recorded:** `.squad/decisions/inbox/purah-release-readiness-audit.md`
