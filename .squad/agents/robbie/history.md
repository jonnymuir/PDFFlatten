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
- Added CLI-focused integration tests for the missing-arguments path and for running the sample against `BAPSL_P60_Populated.pdf`.

**Outcome:**
The sample app now runs end-to-end with input/output file arguments, produces a flattened PDF from the real fixture, and the suite is green with 13 passing tests.

**Decision Merge (2026-05-14T21:22:32Z):**
- Scribe archived Robbie's console sample review decision into `decisions.md`.
- Console sample contract locked: two-argument CLI (`input.pdf output.pdf`), non-zero exit on bad invocation, usage text printed. Integration tests cover the happy path and error cases.
- Fixture validation: `BAPSL_P60_Populated.pdf` flattened output must have no `/AcroForm` or widget annotations left.
- Orchestration log: `.squad/orchestration-log/2026-05-14T21:22:32Z-Robbie.md`.

