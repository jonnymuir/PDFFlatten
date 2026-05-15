# Squad Decisions

## Active Decisions

- 2026-05-14T21:16:50.701+01:00 — The squad uses Zelda-derived cast names for project agents; Scribe and Ralph remain fixed system roles.
- 2026-05-14T21:16:50.701+01:00 — PDFFlatten targets VB.NET on .NET Framework 4.6.2 and is focused on flattening PDF AcroForm fields for reliable iPhone printing.

- 2026-05-14T21:25:31.595+01:00 — PDFFlatten is scaffolded as a VB.NET class library targeting .NET Standard 2.0 so it builds on macOS via the dotnet CLI; PDF library selection is intentionally deferred until compatibility is reviewed.
- 2026-05-14T21:25:31.595+01:00 — User directive: build PDFFlatten as a portable .NET library instead of .NET Framework 4.6.2; Purah specializes in portability/runtime-compatibility decisions.
- 2026-05-14T21:25:31.595+01:00 — .NET Standard 2.0 chosen over .NET Framework (Windows-only, incompatible with macOS), .NET Standard 2.1 (breaks Framework consumers), or Modern .NET (loses portability); broadest reach across all .NET implementations while supporting macOS CLI builds.

## Recent Decisions (2026-05-14 Batch)

### User Directives
- 2026-05-14T21:39:55.268+01:00 — Implement PDF flattening in-house rather than relying on a restrictive third-party engine; the library should parse PDFs itself, detect AcroForm fields, and flatten them comprehensively.
- 2026-05-14T21:39:55.268+01:00 — Package this library as the NuGet package `PDFFlatten`, expose an initial `Flatten(Stream) -> Stream` API, add strong tests, GitHub-ready docs and structure, GitHub Actions for CI/CD and NuGet publishing, and include guidance for creating the GitHub repository.

### Impa (Productization)
- 2026-05-14T21:39:55.268+01:00 — PDFFlatten productization: Ship around a single public NuGet package named `PDFFlatten` targeting `netstandard2.0`, keep the PDF engine behind an explicit adapter seam, maintain `src/PDFFlatten`, `tests/PDFFlatten.Tests`, optional `tests/PDFFlatten.TestData`, root docs/config, and split GitHub Actions into CI (build/test/pack validation) and release/publish workflows.
- 2026-05-14T21:39:55.268+01:00 — Productize PDFFlatten as a GitHub-ready NuGet library with a single primary public entry point (`PdfFlattener.Flatten`), a dedicated NUnit regression suite, `CI` and `Release` GitHub Actions workflows, and baseline repo hygiene files (README, CHANGELOG, CONTRIBUTING, MIT licence, setup docs). Keep release automation tag-driven (`v*`) and use `NUGET_API_KEY` as the only required publishing secret.

### Purah (Public API)
- 2026-05-14T21:39:55.268+01:00 — PDFFlatten exposes a single primary public entry point, `PdfFlattener.Flatten(Stream) As Stream`, plus a caller-owned output-stream overload. Package remains `netstandard2.0`, NuGet-ready, and avoids restrictive third-party PDF libraries by flattening AcroForm widgets in-house.

### Zelda (Flattening Engine)
- 2026-05-14T21:39:55.268+01:00 — First in-house flattening path: Reuse each widget's existing normal appearance stream as a page XObject, append draw commands into page contents, remove widget annotations from page annotation arrays, drop the catalog `/AcroForm`, and serialize only objects still reachable from the trailer.

### Robbie (Tests)
- 2026-05-14T21:39:55.268+01:00 — Regression suite uses `BAPSL_P60_Populated.pdf` as the primary real-world fixture and asserts its expected AcroForm field names and populated values before trusting any flattening result claims.
- 2026-05-14T21:39:55.268+01:00 — Public `Flatten(Stream)` behavior is covered through reflection-based contract tests so CI stays green while the API is still absent, then automatically exercises the real entry point once it exists.
- 2026-05-14T21:39:55.268+01:00 — macOS CI should run `dotnet test PDFFlatten.sln`; the test project targets `net8.0` and copies the sample PDF into the test output so no machine-specific paths are required.

### Purah (Console Sample)
- 2026-05-14T22:18:01.321+01:00 — Add a minimal VB.NET console sample at `samples/PDFFlatten.Sample` targeting `net8.0`, reference the `src/PDFFlatten` library project directly, and keep the library API unchanged by calling `PdfFlattener.Flatten(input, output)`. Targeting `net8.0` is straightforward to run locally with the current `dotnet` CLI on macOS; VB.NET mirrors the library's intended usage style; project reference keeps the sample honest against in-repo library.
- 2026-05-14T22:18:01.321+01:00 — Console sample workflow is a simple two-argument contract: `input.pdf output.pdf`, with non-zero exit and usage text for bad invocation. Keep the real fixture `BAPSL_P60_Populated.pdf` as proof path; flattened output must have no `/AcroForm` or widget annotations left. Lock command-line behavior with automated integration coverage.

### Zelda & Robbie (Fixture Replacement)
- 2026-05-14T22:30:41.645+01:00 — Replace the removed sensitive regression PDF with `GenericAcroFormFixture.pdf`, a checked-in synthetic one-page AcroForm sample at the repo root. Fixture scope is narrowly aligned to the current flattening contract: classic xref table, direct page `/Annots`, widget annotations that carry `/T` and `/V`, and normal appearance streams at `/AP /N`. This proves the existing flattening path without reintroducing sensitive content.
- 2026-05-14T22:30:41.645+01:00 — Active tests and README examples depend on the generic `GenericAcroFormFixture.pdf` sample only for structural behavior: it must be a valid populated AcroForm PDF with known generic field/value pairs and normal appearance streams; flattening it must remove `/AcroForm`, remove widget annotations, and emit one appearance draw per field. This keeps regression coverage meaningful without baking business-specific data into active quality gates.

### Zelda (Appearance Resource Repair)
- 2026-05-14T22:36:43.725+01:00 — When flattening text widgets, PDFFlatten may not trust a normal appearance stream's font resources blindly. If the appearance content references font names that do not resolve from that appearance stream's `/Resources`, the flattener repairs the appearance resource dictionary before painting it onto the page. Decode plain or `/FlateDecode` appearance streams to inspect `Tf` font operands. For text fields (`/FT /Tx`), repair missing appearance font mappings from the widget's own `/DA` + `/DR` when possible. If no widget font can be borrowed, fall back to a narrow set of standard Acrobat font aliases (`Helv`, `HeBo`, `HeOb`, `HeBO`, `Cour`, `CoBo`, `CoOb`, `CoBO`, `TiRo`, `TiBo`, `TiIt`, `TiBI`, `ZaDb`).

### Robbie (Appearance Resource Regression)
- 2026-05-14T22:36:43.725+01:00 — Regression coverage must assert renderability, not just widget removal or `/Do` count. Flattening can look structurally successful while still dropping visible field text if the replayed appearance XObjects do not carry usable font resources after `/AcroForm` is removed. For each flattened `FldFlat*` appearance XObject, every font named by a `Tf` operator must still resolve from the XObject or page `/Resources`. Use a generic synthetic text-field fixture whose widget appearance depends on form-level font resources.

### Impa (Release v0.1.0)
- 2026-05-14T22:55:18.041+01:00 — Release v0.1.0 as the first public release on NuGet.
  - **Product Completeness:** The library implements end-to-end PDF AcroForm flattening with in-house PDF parser/serializer, widget appearance reuse strategy with orphan pruning, text appearance font resource repair, and 15 passing regression tests.
  - **Release Readiness:** All 15 NUnit tests passing, package builds cleanly, GitHub Actions CI/CD in place, sealed API contract, complete NuGet metadata, documentation in place, repository follows conventions, NUGET_API_KEY configured as GitHub secret, SourceLink configured.
  - **Version Selection:** v0.1.0 chosen for first public release; patch 0 signals initial release with experimental API.
  - **Outcome:** Committed release-ready changes to main (commit 282a88d), pushed 4 commits to origin/main, created and pushed v0.1.0 tag, GitHub Actions Release workflow triggered. Release workflow completed successfully: GitHub release created at https://github.com/jonnymuir/PDFFlatten/releases/tag/v0.1.0, NuGet packages published (PDFFlatten.0.1.0.nupkg and PDFFlatten.0.1.0.snupkg), all 15 regression tests passed in CI, package now live on NuGet.org for public consumption.
  - **Co-authored:** Impa (Lead), with orchestration and testing work from Zelda, Purah, and Robbie.

## Post-Release Audit (2026-05-14 Batch)

### Zelda (PDF-Spec Production-Readiness)
- 2026-05-14T23:04:38.903+01:00 — PDFFlatten v0.1.0 is not safe for broad "any PDF" use; only production-safe for a narrow AcroForm slice.
  - **Supported scope:** Classic xref-table PDFs, unencrypted files, page dictionaries with direct `/Annots` arrays, existing widget `/AP /N` appearance streams already visually correct, pages that do not depend on inherited page resources, rotation, appearance matrices, or stateful appearance dictionaries.
  - **Why this is the supported slice:** Parser only accepts classic `xref` tables and direct-integer stream `/Length` values; flattening replays existing widget appearances; placement uses only `/Rect` and `/BBox` without `/Matrix`, rotation, or page rotation; mutates page `/Resources` without collision-avoidance or inheritance merge.
  - **Concrete risks:** Indirect `/Annots` arrays fail outright; blank pages with widgets fail; inherited `/Resources` can be silently overridden; existing page XObjects can be overwritten with collision names; checkbox/radio appearance dictionaries not supported.
  - **Required guardrails before claiming broad production readiness:** Reject `/Encrypt`, `/XFA`, xref-stream, object-stream, incremental-update PDFs; reject non-stream `/AP /N`; reject pages with inherited `/Resources` unless safely merged; handle rotated widgets and appearance matrices; add producer-diverse fixtures with render-level validation.

### Robbie (Test/Coverage Production-Readiness)
- 2026-05-14T23:04:38.903+01:00 — Current 15-test suite proves a narrow contract only; major real-world failure surfaces remain unguarded.
  - **Protected today:** API contract around null handling, stream ownership, rewindable returned stream; no-op for plain PDFs; `/AcroForm` and widget annotation removal for generic fixture; non-widget annotation preservation; one appearance draw per field for generic fixture; CLI sample argument handling; text appearance font repair for one synthetic case.
  - **High-risk assumptions not protected:** Xref-stream and object-stream PDFs unsupported/untested; indirect stream `/Length` unsupported/untested; inherited page attributes assumed away; indirect `/Annots` unsupported/untested; state-driven appearances untested; page rotation and appearance `/Matrix` untested; incremental-update PDFs untested; field hierarchy inheritance untested; non-Flate/multi-filter appearance streams effectively untested; no render-level assertion beyond narrow font-resource checks.
  - **Testing decision:** Before claiming broad production readiness, add regression fixtures for xref-stream rejection, indirect `/Length`, inherited page resources, indirect `/Annots`, checkbox/radio appearance-state widgets, rotated pages, incremental-update forms, field hierarchy inheritance, non-Flate appearance streams; add renderability-oriented checks per risk bucket beyond structural assertions.

### Impa (Overall Production-Readiness Judgment)
- 2026-05-14T23:04:38.903+01:00 — PDFFlatten v0.1.0 safe only for a constrained slice; not safe for broad production use.
  - **Verdict:** Credible as a narrow utility for classic AcroForm PDFs with usable widget normal appearance streams fitting the v0.1 scope; not robust for broad production across arbitrary producer PDFs, updated PDFs, signed PDFs, rotated pages, inherited-resource page trees, or field types outside the synthetic fixture.
  - **What is strong:** Small stable public API; professional packaging/release posture with CI, release workflow, tagged release, NuGet metadata; clean meaningful tests for implemented slice; honest README scoping classic xref PDFs with widget `/AP /N` reuse.
  - **What blocks broad-production claim:** Intentionally narrow parser support (no xref streams, object streams, `/Prev` chain, non-direct integer lengths); specific widget model assumptions (direct page `/Annots`, indirect stream `/AP /N`, placement via `/Rect`/`/BBox` only); real corruption/mis-render risks (inherited `/Resources` override, missing reference tolerance, ASCII-only serialization); verification depth too narrow (15 tests, synthetic fixture dominated, no producer corpus, no render-diff validation).
  - **Highest-risk failure modes:** Incorrect rendering on rotated/transformed pages; original page content breaks with inherited resources; common real-world forms fail hard; text stays blank or wrong font; structurally incomplete output; non-ASCII metadata incorrectly rewritten; signed/encrypted/special PDFs unsafe.
  - **Minimum guardrails before claiming production safety:** Documentation guardrail (state classic xref-table scope, explicitly exclude signed/encrypted/xref-stream/object-stream/incremental/transform-heavy/state-appearance); runtime fail-closed checks (reject `/Prev`, xref streams, object streams, encrypted, signatures, indirect `/Annots`, non-stream `/AP /N`, unresolved references, inherited page resources, unsupported transforms); correctness guardrail (do not serialize if any resource/font/reference unresolvable); verification guardrail (real-world corpus from multiple producers, renderer/viewer comparison checks, negative tests for unsupported inputs).
  - **Release judgment:** Keep v0.1.0 as early constrained utility release, not general-purpose engine; ship with explicit input constraints and fail-closed behavior outside that slice.

### Impa (Production-Readiness Audit → Issue Backlog)
- 2026-05-15T06:07:34.849+01:00 — Production-readiness audit converted to actionable GitHub issues.
  - **Converted decisions:** Zelda's PDF-spec guardrails audit, Robbie's test-coverage gaps audit, Impa's overall production-readiness judgment → three focused GitHub issues (#2, #3, #4).
  - **Issue #2:** Parser hardening: Add fail-closed guards for unsupported PDF variants (Zelda/Purah owned, blocks #3)
  - **Issue #3:** Expand regression suite with unsupported-input fixtures and renderability checks (Robbie owned)
  - **Issue #4:** Documentation: Establish production-readiness boundaries and update README (Impa/Purah owned, post #2/#3)
  - **Sequencing:** #2 unblocks #3; both complete before v0.2.0 production-readiness claim.
  - **Not addressed:** Appearance matrix rotation, field hierarchy flattening, producer-specific rendering quirks — tracked separately as future enhancements.

### Purah (Language Port)
- 2026-05-15T06:16:04.770+01:00 — C# netstandard library port.
  - **What:** Port `src/PDFFlatten` from VB.NET to C# while keeping the package on `netstandard2.0`, move the solution/workflows/docs to `PDFFlatten.csproj`, and keep the sample/test surface aligned with the C# source layout.
  - **Why:** Jonny asked for C# source without sacrificing .NET Framework/current .NET reach. Keeping `netstandard2.0` preserves the compatibility matrix.
  - **Outcome:** Ported one-class-per-file, updated solution/sample/docs/squad artifacts, validated build/test/pack successfully. Semantics and scope unchanged; PDF-semantic review reserved for Zelda.

### Robbie (Test/CI Migration)
- 2026-05-15T06:16:04.770+01:00 — Net10 test migration guardrails.
  - **What:** Moved `tests/PDFFlatten.Tests` to `net10.0`, kept the regression intent unchanged, and made the test wiring tolerant of the in-flight language port by resolving `PDFFlatten.csproj` first and falling back to `PDFFlatten.vbproj` only if the C# project is not present yet. Moved the sample CLI test output under `artifacts/test-output/...` so the suite stops writing under the test bin folder.
  - **Why:** Quality gate follows the C# migration without forcing concurrent edits. Sample smoke test needs a stable output path on `net10.0`.

- 2026-05-15T06:16:04.770+01:00 — Sample/docs coverage judgment for the C# port.
  - **What:** Kept the existing CLI integration coverage as the executable proof for the sample app after the port. Did not add README-snippet compilation tests; current documentation examples still exercise the same `PdfFlattener.Flatten` contract already covered by the API and sample tests.
  - **Why:** Test coverage protects behavior, not maintenance tax. Meaningful regression risk is "does the shipped sample still run end-to-end?" — that path is now green on `net10.0`.

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
