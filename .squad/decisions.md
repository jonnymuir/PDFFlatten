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

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
