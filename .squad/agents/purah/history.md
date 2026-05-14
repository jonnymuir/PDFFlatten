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
