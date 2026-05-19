# Project Context

- **Owner:** Jonny Muir
- **Project:** PDFFlatten
- **Stack:** C#, .NET Standard 2.0 class library, PDF AcroForm/form-field flattening
- **Description:** A C# utility library that flattens PDF form fields into printable page content while staying broadly portable across .NET runtimes.
- **Created:** 2026-05-14T21:16:50.701+01:00

## Learnings

- I own C# implementation details and cross-runtime targeting decisions.
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
- **Robbie:** 11 passing regression tests; Downloads-only populated real-form fixture assertions; macOS-safe CI.

**Purah's Role in Batch:**
Locked down the public surface: `PdfFlattener.Flatten(Stream)` contract, output-stream overload, package metadata. This stable API shields Zelda's implementation from thrashing and allows Robbie to write lasting tests.

**Next for Purah:**
API is stable for the foreseeable future. Future enhancements (e.g., field value rendering, edge case modes) can evolve behind this interface without breaking consumers.

### 2026-05-14T22:18:01.321+01:00 — Console sample added

**Context:** Jonny wanted a local command-line sample that exercises the library against the PDF already in the repo.

**Work completed:**
- Added `samples/PDFFlatten.Sample`, a minimal VB.NET `net8.0` console app wired into `PDFFlatten.sln`.
- Implemented two-argument CLI handling (`input-file`, `output-file`) that calls `PdfFlattener.Flatten(input, output)` without changing the public API.
- Updated `README.md` with a direct `dotnet run --project ...` example for a Downloads-only populated real form.

**Takeaway:** For portability-first libraries, keep runnable examples on a modern executable target like `net8.0` while leaving the reusable library on `netstandard2.0`; that gives a friction-free local smoke-test path without sacrificing package reach.

**Decision Merge (2026-05-14T21:22:32Z):**
- Scribe archived Purah's console sample decision into `decisions.md`.
- Console sample decision locked: add `samples/PDFFlatten.Sample` (VB.NET, net8.0), target two-argument CLI (`input.pdf output.pdf`), call `PdfFlattener.Flatten(input, output)`.
- Robbie validates end-to-end with real fixture; integration tests ensure command-line contract holds.
- Orchestration log: `.squad/orchestration-log/2026-05-14T21:22:32Z-Purah.md`.

### 2026-05-14T22:33:44.877+01:00 — Downloads-based sample run verified

**Context:** Jonny moved the real-world PDF into `~/Downloads` and wanted the sample app run there so the flattened result could be tested from a phone.

**Work completed:**
- Resolved the hinted filename hint for the Downloads-only populated real form to the single clear Downloads match.
- Ran `dotnet restore`, `dotnet build PDFFlatten.sln --configuration Release`, and `dotnet test PDFFlatten.sln --configuration Release` successfully before executing the sample.
- Executed the VB.NET console sample against the Downloads PDF and produced `~/Downloads/flattened.pdf`.

**Runtime note:** The current local smoke-test path is portable and straightforward on macOS: keep the reusable library at `netstandard2.0`, but run the sample through the installed `dotnet` SDK as a `net8.0` console app when validating a user-supplied PDF outside the repo.

### 2026-05-14T22:33:44.877+01:00 — Sample run orchestration complete

**Session:** Scribe orchestration for Zelda & Purah spawn manifest

**Work completed:**
- Downloaded populated real form successfully flattened via console sample to `~/Downloads/flattened.pdf`.
- All builds and tests clean (13/13 regression suite passing).
- Orchestration log: `.squad/orchestration-log/purah-2026-05-14.log`.
- Sample end-to-end workflow documented and locked.

**Status:** Console sample is a stable, repeatable tool for testing the library against real user PDFs on the developer's machine. API remains unchanged.

