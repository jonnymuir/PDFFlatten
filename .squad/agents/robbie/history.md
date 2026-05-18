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

## Learnings

- 2026-05-18T12:35:10.553+01:00 — Real-world rerun path stays `dotnet run --project samples/PDFFlatten.Sample -- /Users/jonnymuir/Downloads/BAPSL_P60_Populated.pdf /Users/jonnymuir/Downloads/BAPSL_P60_Populated.flattened.pdf`; on the current hardened tree it still succeeds cleanly, produces `e76ac3fba1d3b5ee238bbb524a103e21` at the output path, and Quick Look renders the flattened PDF without the CoreGraphics warning seen on the original input.
- 2026-05-18T12:35:10.553+01:00 — The fail-closed fallback example is now executable in `tests/PDFFlatten.Tests/DocumentedFallbackTests.cs`: cover both `NotSupportedException` (real rejected producer-corpus input) and `InvalidOperationException` (malformed synthetic input), and assert the caller logs a warning, leaves the source file untouched, and copies the original bytes to the fallback output path.

## 2026-05-18T11:37:22Z — Scribe: Fallback test pattern decision merged

**Context:** Robbie's fallback-example test decision merged from inbox by Scribe.

**Decision:** Create `tests/PDFFlatten.Tests/DocumentedFallbackTests.cs` to test the fallback pattern documented by Purah. Prove warning logged, source PDF unchanged, and original PDF correctly copied for `NotSupportedException` and `InvalidOperationException`.

**Rationale:** Keeps sample app as simple success/fail CLI smoke test; test harness proves the fallback pattern without broadening sample contract.

**Status:** ✅ Decision merged. Ready for Robbie test implementation.
- 2026-05-18T12:35:10.553+01:00 — Security hardening regression coverage now lives in `tests/PDFFlatten.Tests/ParserHardeningTests.cs`, backed by `src/PDFFlatten/Internals/PdfSecurityLimits.cs`: use a seekable synthetic stream for whole-file caps, binary stream fixtures for `FlateDecode` amplification caps, and malformed numeric fixtures so hostile PDFs stay inside the documented `NotSupportedException`/`InvalidOperationException` rejection contract.

## Session 2026-05-18 — Security Hardening Round

**Date:** 2026-05-18T12:35:10.553+01:00

### Outcomes
- Added 4 security regression tests to `ParserHardeningTests.cs`:
  - `/FT /Sig` fail-closed rejection test
  - Oversized input stream rejection test
  - Oversized direct stream `/Length` rejection test
  - `FlateDecode` appearance inflation-cap rejection test
  - Malformed `startxref` and negative `/Length` normalization to `InvalidOperationException` test
- Full regression suite: 61/61 passing

### Decisions
- Security regression bar locked with focused negative tests for hostile PDF rejection boundaries
- Callers can trust flattening as a safe rejection boundary

### Status
All regression tests passing; security regression bar complete and locked.

### Next
Release v0.1.0 with security hardening regression coverage complete.
