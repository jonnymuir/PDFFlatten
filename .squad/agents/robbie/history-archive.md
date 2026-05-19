# Project Context

- **Owner:** Jonny Muir
- **Project:** PDFFlatten
- **Stack:** VB.NET, .NET Framework 4.6.2, PDF AcroForm/form-field flattening
- **Description:** A VB.NET utility that takes PDFs with form fields and flattens them so they print correctly from an iPhone.
- **Created:** 2026-05-14T21:16:50.701+01:00

## Learnings

- I own regression coverage, tricky sample PDFs, and printability validation for flattened output.
- The core user outcome is not just flattening a form technically, but producing a PDF that survives iPhone printing without interactive-field issues.

- Added a macOS-safe NUnit regression harness that characterises the real sample PDF and auto-hooks into a future public `Flatten(Stream)` entry point without hard-coding a concrete implementation class.

## 2026-05-14T21:39:55Z — Team Batch Complete

**Peer Outcomes:**
- **Impa:** Repo productization complete (GitHub docs, workflows, SourceLink, release automation).
- **Purah:** `PdfFlattener.Flatten(Stream)` contract locked; package metadata finalized; NuGet build validated.
- **Zelda:** In-house PDF flattening engine live; widget appearance reuse; orphan pruning; functional end-to-end.

**Robbie's Role in Batch:**
Wrote the regression suite that validates the entire stack. Fixture-backed assertions lock in expected behavior (field names, values) before and after flattening. 11 tests passing confirms integration across Purah + Zelda + packaging.

**Next for Robbie:**
Regression suite is the quality gate. All future work must keep tests green. Additional fixtures (edge cases, different PDF structures) can be added incrementally. Performance and flattening quality remain the primary success metrics.

## 2026-05-14T22:18:01.321+01:00 — Console sample verification

**What I checked:**
- Ran the existing regression suite (`dotnet test PDFFlatten.sln --configuration Release`) to confirm baseline quality before reviewing the sample workflow.
- Reviewed the new `samples/PDFFlatten.Sample` console project, its README invocation, and its command-line contract.
- Added CLI-focused integration tests for the missing-arguments path and for running the sample against a Downloads-only populated real form.

**Outcome:**
The sample app now runs end-to-end with input/output file arguments, produces a flattened PDF from the real fixture, and the suite is green with 13 passing tests.

**Decision Merge (2026-05-14T21:22:32Z):**
- Scribe archived Robbie's console sample review decision into `decisions.md`.
- Console sample contract locked: two-argument CLI (`input.pdf output.pdf`), non-zero exit on bad invocation, usage text printed. Integration tests cover the happy path and error cases.
- Fixture validation: the Downloads-only populated real-form flattened output must have no `/AcroForm` or widget annotations left.
- Orchestration log: `.squad/orchestration-log/2026-05-14T21:22:32Z-Robbie.md`.

## 2026-05-14T22:30:41.645+01:00 — Generic fixture audit

**What I checked:**
- Audited the tests, sample CLI path, README usage text, and squad records for assumptions tied to the removed sensitive fixture.
- Tightened the regression suite around generic fixture invariants instead of hard-coding the old domain-shaped sample expectations.
- Recorded which remaining references are historical notes versus active test or documentation dependencies.

**Outcome:**
The active suite now depends on a generic populated AcroForm sample and proves the important behavior: populated widgets exist before flattening, flattening removes `/AcroForm` and widget annotations, and the page replays one appearance draw per field.

**Learning:**
- When a fixture must be swapped for privacy reasons, preserve only the structural contract the tests need and strip business-specific names, values, and fixture lore out of active assertions and docs.

## 2026-05-14T22:36:43.725+01:00 — Missing field values regression

**What I checked:**
- Flattened a Downloads-only populated real form through the sample app and inspected the resulting PDF structure instead of trusting widget removal alone.
- Traced the real observable to flattened appearance XObjects: the suite needed to prove those XObjects still had usable font resources after `/AcroForm` removal.
- Added a generic regression fixture where the widget appearance text depends on form-level font resources, then asserted the flattened output keeps those fonts resolvable.

**Outcome:**
- The regression suite now includes coverage for the quiet failure mode behind “page still there, values missing.”
- Re-running the sample path against the Downloads PDF produced flattened appearance XObjects with embedded/resolvable font resources, and `dotnet test PDFFlatten.sln --configuration Release --no-restore` passed with 15 tests.

**Learning:**
- Counting replayed appearance draws is not enough; a flattened text appearance is only trustworthy if the fonts referenced by its `Tf` operators are still resolvable in the final PDF.

## 2026-05-14T21:48:37Z — Appearance Resource Regression Complete

**Team Outcome:**
- **Zelda** repaired broken appearance font resources in the flattening engine.
- **Scribe** merged decisions and logged orchestration for the session.

**Session Result:**
All 15 regression tests passing. The test suite now includes `AppearanceResourceRegressionTests` asserting that every font named by a `Tf` operator in a flattened appearance XObject remains resolvable from that XObject or page `/Resources`. Real-world PDF validation confirms the library correctly renders field values in flattened output.

## 2026-05-14T23:04:38.903+01:00 — Production readiness audit

**What I checked:**
- Reviewed the README promises, sample path, current NUnit suite, synthetic fixtures, parser, serializer, and flattening code paths.
- Ran the full repository test suite to confirm the current baseline before judging confidence.
- Mapped what the suite proves versus the PDF structures the engine explicitly assumes away.

**Outcome:**
- All 15 tests pass, but they only justify confidence in a narrow happy path: classic-xref AcroForm PDFs with direct annotation arrays, simple appearance streams, and the checked-in synthetic fixture/sample flow.
- The biggest unguarded failure surface is unsupported-but-valid PDF structure: xref streams/object streams, indirect stream lengths, inherited page resources/rotation, indirect `/Annots`, appearance-state widgets, incremental updates, and field-hierarchy inheritance.
- Production claim for arbitrary PDFs would be overconfident; current evidence supports controlled-input usage, not “throw anything at it.”

**Learning:**
- For PDF flattening, structural success (`/AcroForm` gone, widgets gone, `/Do` commands present) is a weak proxy. Production confidence needs fixtures for valid-but-different PDF structures and at least one visibility-preservation check per risk class.


**2026-05-14T23:04:38Z — Post-Release Audit & Coverage Readiness Judgment (Scribe Processing)**
- Coverage audit verdict recorded in `decisions.md`: current 15-test suite proves narrow path only; major real-world failure surfaces remain unguarded
- Test decision: before broad production claim, add fixtures for xref-stream rejection, indirect `/Length`, inherited page resources, indirect `/Annots`, checkbox/radio widgets, rotated pages, incremental-update, field hierarchy, multi-filter appearance streams
- Orchestration log: `.squad/orchestration-log/robbie-2026-05-14T23-04-38Z.md`
- Protected today: API contract, null handling, stream ownership, no-op for plain PDFs, `/AcroForm`/annotation removal, non-widget annotation preservation, one appearance per field, CLI sample, text font repair for synthetic case
- High-risk unprotected: xref/object-stream PDFs untested, indirect `/Length` untested, inherited page attributes assumed away, indirect `/Annots` untested, state-driven appearances untested, page rotation/appearance `/Matrix` untested, incremental-update untested, field hierarchy untested, multi-filter streams untested, no render-level assertions
- Recommendation: add renderability checks per risk bucket so "widgets removed" cannot masquerade as "content preserved"

## 2026-05-15T05:07:34Z — Production-Readiness Audit → GitHub Issue #3

Impa converted Robbie's test-coverage audit findings into GitHub Issue #3: **Expand regression suite with unsupported-input fixtures and renderability checks**

**Issue #3 Scope:**
- Owned by: Robbie (test infrastructure)
- Add ~10-15 new regression fixtures covering unsupported-input negatives, producer diversity, and renderability assertions
- Acceptance: 25-30 total tests (up from 15), clear rejection on unsupported inputs, fonts/resources verify post-flattening
- Blocker dependency: unblocked by Issue #2 (parser hardening must land first so exceptions work)

**Rationale:** Current 15 tests cover only synthetic slice; need real-world producer diversity and renderability depth to claim production safety. Negative tests for xref-stream/object-stream/indirect-annots/etc require parser hardening to be in place.

**Sequencing:** Issue #2 (Zelda: parser hardening) complete → Issue #3 (Robbie: test coverage) in parallel with Issue #4 (Impa/Purah: documentation) → before v0.2.0

## 2026-05-15T06:16:04.770+01:00 — Net10 verification pass for C# migration (updated context)

**What I checked (original):**
- Ran the existing solution tests first and treated the result honestly instead of assuming the suite was clean; baseline had one failing CLI integration test caused by writing the sample output under the test bin folder and tripping a file lock.
- Moved `tests/PDFFlatten.Tests` to `net10.0` and updated the test-side project resolution so the suite can follow the library/sample cutover from VB project files to C# project files without me editing Purah-owned implementation files.
- Re-ran the release build and full solution test suite after the changes.

**Outcome (original):**
- The regression suite stays at 15 tests with the same behavioral intent and now passes on `net10.0`.
- The sample smoke test is stable again because it writes to `artifacts/test-output/...` instead of the test output directory.
- No .NET 10 tooling blocker showed up on this machine; SDK `10.0.102` built and tested the repo successfully.

**Learning:**
- For migration work, brittle test plumbing is as dangerous as broken assertions; if the quality gate depends on file paths or project-file extensions, make the harness resilient before blaming the implementation.

**2026-05-15T05:33:44Z — C# migration validated (Scribe merge):**
- Language port from VB to C# complete with one-class-per-file structure
- All 15 regression tests still passing on `net10.0` post-migration
- Sample CLI integration tests stable with consolidated output path
- Public API (`PdfFlattener.Flatten`) semantics preserved; no behavior regression
- Test harness successfully resolved `PDFFlatten.csproj` (C# primary) instead of .vbproj
- Next context: Zelda PDF-semantic review; Robbie test expansion for unsupported-input guards

## 2026-05-15T07:04:03.456+01:00 — Production-Hardening Milestone (Issue #3) Complete

**Executive Summary:** Issue #3 (Test Coverage Expansion) complete, committed, and production-quality. Test suite expanded from 15 to 37 tests with comprehensive negative-case coverage and renderability assertions.

**Test Breakdown (37 total):**
- FlattenApiContractTests.cs (3) — API null handling, stream ownership, rewindability
- PdfFlattenerTests.cs (6) — Core flattening logic for supported slice
- ParserHardeningTests.cs (9) — Negative tests for each unsupported structure
- SamplePdfCharacterizationTests.cs (1) — Fixture introspection
- FlattenOutputTests.cs (2) — Flattening output verification
- AppearanceResourceRegressionTests.cs (1) — Appearance resource handling
- ConsoleSampleTests.cs (2) — Console sample CLI integration
- UnsupportedPdfGuardTests.cs (6) — Additional unsupported-input guards + **renderability checks**
- ExpandedCoverageTests.cs (7) — Rotated pages, appearance matrices, field hierarchies, state-appearance PDFs, multi-filter streams

**Critical: Renderability Assertions**
Each flattened appearance XObject (`FldFlat*`) is verified to have:
- All fonts referenced in `Tf` operators still resolvable from XObject or page `/Resources`
- All XObject references reachable from page `/Resources /XObject`
- No orphaned references post-`/AcroForm` removal

This protects against silent text/content loss when appearance fonts or resources are incorrectly dropped during flattening.

**Quality Signals:**
- ✅ 37 tests passing (all CI green on macOS)
- ✅ Both positive (supported) and negative (unsupported) cases covered
- ✅ Renderability checks catch resource-resolution bugs
- ✅ Synthetic fixtures created for each unsupported variant
- ✅ No regression in existing 15 tests
- ✅ Test suite runs cleanly with no warnings

**Owned By:** Robbie (test strategy, fixture curation, renderability assertions)

**Shared Context:** Zelda's parser guards (Issue #2) now allow negative tests to verify correct rejection behavior. Impa coordinated the hardening sequence. Purah will document scope (Issue #4).
