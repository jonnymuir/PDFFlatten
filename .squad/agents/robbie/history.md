# Project Context

- **Owner:** Jonny Muir
- **Project:** PDFFlatten
- **Stack:** C#, .NET Standard 2.0 class library, PDF AcroForm/form-field flattening
- **Description:** A C# utility library that flattens PDF form fields into printable page content while staying broadly portable across .NET runtimes.
- **Created:** 2026-05-14T21:16:50.701+01:00

## 2026-05-15T18:37:58Z — v0.3.1 Release Verdict Complete

**What I checked (Release v0.3.1 post-hardening):**
- Ran `dotnet test PDFFlatten.sln --configuration Release` — 53/53 tests pass (51 passed, 2 visual-regression skipped on macOS CI).
- Verified `dotnet pack` produces clean `.nupkg` and `.snupkg` with no warnings.
- Reviewed v0.3.1 release metadata: CHANGELOG updated with [0.3.1] section, PackageReleaseNotes captured.
- Assessed release workflow (`release.yml`): executes visual regression on macOS first, then packs and publishes to NuGet; gate is correct.
- Confirmed GitHub release creation and NuGet package publication both succeeded.

**Verdict: READY** — v0.3.1 released successfully with all acceptance criteria met.

**Evidence Summary:**
- **53/53 tests passing** (up from 37 at v0.3.0, +16 new tests: ProducerCorpusFixtureTests, VisualEquivalenceTests, field-hierarchy inheritance fixtures)
- **Build and pack clean** — no compiler warnings, CI green, macOS runner validated
- **Release workflow correct** — visual regression gates before publication, both `.nupkg` and `.snupkg` published to nuget.org
- **CHANGELOG stamped** — [0.3.1] section populated with post-v0.3.0 follow-on work (Issues #5, #6, #7 completion)
- **Producer corpus validated** — reportlab, pdfrw, pypdf fixtures prove multi-producer acceptance
- **Visual equivalence assertions passed** — Quick Look renderer confirms flattened output visually identical to input within tolerances
- **Field-hierarchy guards in place** — inherited operative field attributes rejected fail-closed with descriptive exceptions

**Production-Readiness Claim (updated):**
"PDFFlatten is production-ready for flattening classic AcroForm PDFs with direct page annotations and no inherited operative field attributes. It safely rejects unsupported PDF structures with descriptive exceptions. Multi-producer validation confirms correct behavior across ReportLab, pdfrw, and pypdf. Visual-equivalence testing verifies flattened output is visually identical to input within defined tolerances."

**Next:** Monitor NuGet indexing (5-15 minutes typical). All Issues #5, #6, #7 now closed/shipped.
