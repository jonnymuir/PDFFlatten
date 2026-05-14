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

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
