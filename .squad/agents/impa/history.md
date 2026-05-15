# Project Context

- **Owner:** Jonny Muir
- **Project:** PDFFlatten
- **Stack:** VB.NET, .NET Framework 4.6.2, PDF AcroForm/form-field flattening
- **Description:** A VB.NET utility that takes PDFs with form fields and flattens them so they print correctly from an iPhone.
- **Created:** 2026-05-14T21:16:50.701+01:00

## Learnings

- Team lead for scoping the implementation, reviewing architecture, and coordinating specialist handoffs.
- Key specialists on this project are Purah for VB/.NET Framework 4.6.2 and Zelda for PDF/AcroForm behavior.
- 2026-05-14T21:25:31.595+01:00 — Rebased the project direction onto a VB.NET `.NET Standard 2.0` class-library skeleton (`PDFFlatten.sln`, `src/PDFFlatten/PDFFlatten.vbproj`) because macOS rules out .NET Framework and the PDF engine choice should stay deferred behind `IPdfFlattener`.

**2026-05-14T20:25:31Z — Decision Merged & Orchestration Complete**
- Scribe merged Impa's project-structure decision into `decisions.md` alongside Purah's portable-target analysis.
- Orchestration log created: `.squad/orchestration-log/2026-05-14T20-25-31-Impa.md`.
- Both Purah and Impa decisions now in team record. Squad is ready for PDF library selection and implementation work.
- 2026-05-14T21:39:55.268+01:00 — Productized the repo for GitHub/NuGet: polished README, added GitHub setup docs, changelog/contributing scaffolds, CI and tag-driven release workflows, SourceLink/symbol packaging, and validated build/test/pack locally.

## 2026-05-14T21:39:55Z — Team Batch Complete

**Peer Outcomes:**
- **Purah:** Delivered `PdfFlattener.Flatten(Stream)` + overload, package metadata, MIT licence, successful NuGet build.
- **Zelda:** Implemented in-house PDF parser/serializer; widget appearance reuse strategy; orphan object pruning; functional flattening path.
- **Robbie:** 11 passing NUnit tests; fixture-backed regression suite; macOS-safe CI; reflection-based contract assertions.

**Impa's Role in Batch:**
Transformed the squad's work into a ship-ready GitHub repository: productization docs, GitHub Actions CI (build/test/pack), tag-driven release workflow, hygiene files (CHANGELOG, CONTRIBUTING, MIT), SourceLink/symbol guidance, and validated end-to-end locally.

**2026-05-14T22:55:18Z — Release v0.1.0 Complete**

- Staged and committed appearance-resource repair work + generic fixture (commit 282a88d)
- Pushed 4 commits to origin/main (from 3 earlier Scribe merges + this release commit)
- Created and pushed v0.1.0 tag; GitHub Actions Release workflow triggered and running
- All 15 NUnit tests passing; build validated
- Decision recorded in `.squad/decisions/inbox/impa-release-v0.1.0.md`
- Release workflow expected to build, test, pack, create GitHub release, and publish to NuGet within 2-3 minutes

**Next for Impa:**
Monitor workflow completion and verify NuGet publication. Repository is ready for v0.1.0 publication.
- 2026-05-14T22:26:30.112+01:00 — Canonical GitHub remote for this repo is `https://github.com/jonnymuir/PDFFlatten.git` on branch `main`; publishing should use that origin directly. Baseline `dotnet test` currently fails because the expected root fixture `BAPSL_P60_Populated.pdf` is absent from the working tree.
- 2026-05-14T23:04:38.903+01:00 — Production-readiness review verdict: PDFFlatten v0.1.0 is safe only for a constrained slice, not for broad arbitrary-PDF production use. Core reasons: parser/serializer support remains narrow (classic xref tables only, no xref/object streams or incremental `/Prev` handling), placement ignores important PDF transforms, inherited page resources can be broken by flattening, ASCII-only non-stream serialization risks content mangling, and the 15-test suite is strong for the synthetic slice but not for broad producer coverage.

**2026-05-14T21:29:56Z — Scribe Session: Decision & Orchestration Processing**
- Impa's GitHub publish decision merged from `.squad/decisions/inbox/` into `decisions.md`
- Orchestration log created: `.squad/orchestration-log/impa-2026-05-14T21-29-56Z.md`
- Session log: `.squad/log/scribe-2026-05-14T21-29-56Z.md`
- Team decisions.md now active as canonical record

**2026-05-14T22:55:18Z — Release v0.1.0 Complete (Scribe Processing)**
- Release decision merged from `.squad/decisions/inbox/impa-release-v0.1.0.md` into `decisions.md`
- Orchestration log: `.squad/orchestration-log/impa-2026-05-14T22-55-18Z.md`
- Session log: `.squad/log/scribe-2026-05-14T22-55-18Z.md`
- PDFFlatten v0.1.0 now available on NuGet.org; GitHub release created; all 15 tests passing

**2026-05-14T23:04:38Z — Post-Release Audit & Production-Readiness Judgment (Scribe Processing)**
- Overall production-readiness verdict recorded in `decisions.md`: safe only for constrained slice, not for broad arbitrary-PDF production use
- Key verdict: v0.1.0 credible for classic AcroForm PDFs with usable widget appearances; not robust for broad production across arbitrary producers, signed PDFs, rotated pages, inherited resources, or non-fixture field types
- Orchestration log: `.squad/orchestration-log/impa-2026-05-14T23-04-38Z.md`
- What is strong: small stable API, professional packaging/release with CI/release workflow/NuGet metadata, clean tests for narrow slice, honest README scope
- What blocks broad-production claim: intentionally narrow parser (no xref/object streams, no `/Prev` chain), specific widget model assumptions, real corruption/mis-render risks (inherited `/Resources` override, ASCII-only serialization), narrow verification depth (15 tests, synthetic fixture only, no producer corpus, no render-diff)
- Minimum guardrails recommended: documentation guardrails (explicit scope, exclusions), runtime fail-closed checks (reject unsupported inputs), correctness verification (no unresolvable resources), verification depth (producer corpus, render-diff checks, negative tests)
- Release judgment: keep v0.1.0 as early constrained utility release, not general-purpose engine; ship with explicit input constraints and fail-closed behavior outside narrow slice

## 2026-05-15T06:07:34Z — Production-Readiness Audit → Issue Backlog

Converted the three production-readiness audit decisions into actionable GitHub issues:

**Issue #2 (Parser hardening):** 10 runtime guards to safely reject unsupported PDF structures (xref-stream, object-stream, incremental updates, encryption, indirect annotations, inherited resources, unresolved references, etc.). Owned by Zelda or Purah.

**Issue #3 (Test coverage):** Expand regression suite from 15 to ~25-30 tests with unsupported-input fixtures (negative tests), producer diversity, and renderability assertions (font/resource resolution). Owned by Robbie.

**Issue #4 (Production documentation):** Update README to establish explicit scope boundaries (supported structures, known limitations, not-recommended-for use cases), add API pre-conditions/exceptions, add CHANGELOG note on v0.1.0 as constrained utility (not general-purpose). Owned by Impa or Purah.

**Sequencing:** Parser hardening (#2) blocks test coverage (#3); both should complete before claiming v0.2.0 production-ready.

**Decision recorded:** `.squad/decisions/inbox/impa-production-issues.md` — includes grouping rationale, owned-by guidance, and what NOT to address (rotation support, field hierarchy, render-diff validation — future if demand warrants).

**Key insight:** The post-release audit revealed that v0.1.0 is an honest, professional early release with strong narrow API and packaging story, but it intentionally ships as constrained utility, not general-purpose engine. These three issues close the gap between "works for the slice we built it for" and "safe to recommend broadly." Issue #2 is the blocker — silent corruption risk is unacceptable.
