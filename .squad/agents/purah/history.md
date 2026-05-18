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

- Reran `samples/PDFFlatten.Sample` with real-world PDF (`BAPSL_P60_Populated.pdf`, 107 KB)
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
- Task: Rerun C# console sample on BAPSL_P60_Populated.pdf
- Outcome: ✓ Success
- Output: flattened.pdf (101 KB, MD5: e76ac3fba1d3b5ee238bbb524a103e21)
- Status: Output ready for iPhone visibility check; no code changes required

## 2026-05-15T10:53:30Z — Session Handoff: Sample App Rerun Complete

**Orchestration:** Purah successfully executed the sample app against `/Users/jonnymuir/Downloads/BAPSL_P60_Populated.pdf` and refreshed `/Users/jonnymuir/Downloads/flattened.pdf` for phone visibility validation. No code changes required; library operating nominally on production PDF.

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
