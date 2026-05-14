# Project Context

- **Owner:** Jonny Muir
- **Project:** PDFFlatten
- **Stack:** VB.NET, portable .NET DLL target (TBD), PDF AcroForm/form-field flattening
- **Description:** A VB.NET utility that takes PDFs with form fields and flattens them so they print correctly from an iPhone.
- **Created:** 2026-05-14T21:16:50.701+01:00

## Learnings

- I own VB.NET implementation details and cross-runtime targeting decisions.
- The app's main technical risk is choosing a PDF approach that both supports AcroForm flattening and works cleanly on the portable .NET target we choose.

### 2026-05-14: macOS Constraint → .NET Standard 2.0 Target

**Context:** User discovered the project runs on macOS. .NET Framework is Windows-only and irrelevant.

**Decision:** Shifted from ".NET Framework 4.6.2" to ".NET Standard 2.0" for the VB.NET class library.

**Rationale:**
- .NET Standard 2.0 is consumable from .NET Framework, .NET Core 2.0+, .NET 5–8, Xamarin, Unity.
- Builds and runs natively on macOS via .NET SDK.
- VB.NET compiler has full support (identical IL as C#).
- PDF ecosystem (iText, PdfSharp, SelectPdf, Aspose, SyncFusion) broadly supports `netstandard2.0`.
- .NET Standard 2.1 and modern .NET lose either Framework or old-platform support; 2.0 is the pragmatic sweet spot.

**Caveats:**
- Must verify chosen PDF library publishes `netstandard2.0` in its NuGet package.
- Build environment is `dotnet` CLI on macOS—no Visual Studio (Windows).
- Distribution model is a NuGet package with `netstandard2.0` declared in `.nuspec`.

**Files:** Decision written to `.squad/decisions/inbox/purah-portable-target.md`.

**2026-05-14T20:25:31Z — Decision Merged**
- Scribe merged this decision into `decisions.md` alongside Impa's project-structure decision.
- Orchestration log created: `.squad/orchestration-log/2026-05-14T20-25-31-Purah.md`.
- Next: Impa and downstream agents now have the portable target confirmed. PDF library selection remains deferred.

### 2026-05-14T21:39:55.268+01:00 — Public package/API landed

**Context:** Jonny wanted PDFFlatten turned into a GitHub-ready NuGet library with a real first public API, tests, docs, and CI while keeping implementation in-house.

**Work completed:**
- Replaced the abstraction-only starting point with `PdfFlattener.Flatten(Stream)` plus an output-stream overload.
- Added a netstandard2.0 in-house PDF parser/rewriter that flattens widget appearances back onto page content for the included sample form.
- Upgraded the project for NuGet packaging, SourceLink, XML docs, README packaging, NUnit tests, and GitHub Actions CI/CD.

**Takeaway:** For portability-first PDF utilities, keep the public API tiny and move PDF complexity behind internal seams so runtime/package decisions stay stable even if the implementation deepens.

## 2026-05-14T21:39:55Z — Team Batch Complete

**Peer Outcomes:**
- **Impa:** Repo productization complete (GitHub docs, workflows, SourceLink, release automation).
- **Zelda:** In-house PDF flattening engine live; widget appearance reuse; orphan pruning; edge cases documented.
- **Robbie:** 11 passing regression tests; `BAPSL_P60_Populated.pdf` fixture assertions; macOS-safe CI.

**Purah's Role in Batch:**
Locked down the public surface: `PdfFlattener.Flatten(Stream)` contract, output-stream overload, package metadata. This stable API shields Zelda's implementation from thrashing and allows Robbie to write lasting tests.

**Next for Purah:**
API is stable for the foreseeable future. Future enhancements (e.g., field value rendering, edge case modes) can evolve behind this interface without breaking consumers.
