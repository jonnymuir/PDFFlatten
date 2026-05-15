# Project Context

- **Owner:** Jonny Muir
- **Project:** PDFFlatten
- **Stack:** C#, .NET Standard 2.0 class library, PDF AcroForm/form-field flattening
- **Description:** A C# utility library that flattens PDF form fields into printable page content while staying broadly portable across .NET runtimes.
- **Created:** 2026-05-14T21:16:50.701+01:00

## 2026-05-15T19:37:58Z — v0.3.1 Release Complete (Follow-on Hardening)

**v0.3.1 (Follow-on Field-Attribute Hardening + Producer Corpus Release)** published to NuGet and GitHub:
- Released Issues #5, #6, #7 follow-on work as v0.3.1 patch release
- Field-hierarchy boundary tightened: inherited operative field attributes now reject fail-closed
- Producer fixture corpus added (reportlab, pdfrw, pypdf) with provenance metadata and regression tests
- All 53 tests passing (51 passed, 2 visual-regression skipped on Linux CI); build validated
- Release workflow succeeded cleanly: GitHub release created, NuGet packages published
- Execution path: CHANGELOG updated, PackageReleaseNotes refreshed, commit 9bbcd66 pushed, tag v0.3.1 created and pushed, workflow completed in ~2 minutes
- Production-readiness claim updated: "production-ready for classic AcroForm PDFs with direct page annotations and no inherited operative field attributes"
- Next: Monitor NuGet indexing. Issues #5, #6, #7 now closed/shipped.
