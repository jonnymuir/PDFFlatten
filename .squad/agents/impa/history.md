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

## Learnings

- 2026-05-18T12:35:10.553+01:00 — Security review: the library's main safety strength is its fail-closed supported slice (`PdfFlattener.cs`, `PdfParser.cs`, `ParserHardeningTests.cs`, `UnsupportedPdfGuardTests.cs`), but two real availability gaps remain: unbounded memory/decompression work (`PdfFlattener.cs` around full-input buffering and `InflateData`) and malformed-input paths that can escape the documented rejection contract as raw `OverflowException`/`OutOfMemoryException`.
- 2026-05-18T12:35:10.553+01:00 — Flattening is a stream-only API with no intrinsic file/path attack surface; documented fallback/file handling lives in tests/docs (`DocumentedFallbackTests.cs`) rather than the library surface.
- 2026-05-18T12:35:10.553+01:00 — The serializer keeps all reachable non-form objects (`PdfSerializer.cs`), so flattening should be treated as form-flattening only, not PDF sanitization.
- 2026-05-19T09:10:17.027+01:00 — “Indirect objects everywhere” is the wrong roadmap frame for PDFFlatten. The parser already accepts indirect references as values, but the flattener only resolves them at selected keys, so the maintainable path is targeted dereference support at high-value hot keys (`PdfFlattener.cs`, `PdfParser.cs`) rather than a blanket auto-resolution layer.
- 2026-05-19T09:10:17.027+01:00 — The next real compatibility wins after single-hop indirect `/Length` are small parser/consumer widenings, not broad PDF-format expansion: leading-dot numeric tokens (producer corpus raw fixtures), then selected indirect page/widget containers. Xref streams, inherited page-resource merging, and signed/XFA/encrypted workflows remain bad near-term trades for this library.
- 2026-05-19T09:10:17.027+01:00 — Indirect `/Length` is a parser capability issue, not merely a product-policy issue: without resolving it, the engine cannot safely delimit stream bytes. Pass-through of the original PDF is still a valid outer fallback (`DocumentedFallbackTests.cs`, README fallback pattern), but it must be described as rejection handling, not successful flattening.

## 2026-05-18T11:35:10Z — Security & Signature Audit Complete

**Session:** Post-release security/architecture review (Impa lead, Zelda PDF-spec).

**Key Decisions:**
1. **Security Posture:** Documented fail-closed AcroForm flattener posture, not PDF sanitizer. Next hardening priorities: resource limits (unbounded buffering, `/Length`-driven allocations, `FlateDecode` inflation) and exception normalization (malformed-input paths → documented contract).
2. **Signature Boundary:** `/FT /Sig` widgets now reject fail-closed (Zelda implementation). Replaying signature appearance while removing AcroForm destroys verification semantics—unacceptable success.

**Outcome:** 
- Two decisions logged to `.squad/decisions.md` (2026-05-18 batch)
- Orchestration logs written for both Impa and Zelda
- Session log: `.squad/log/2026-05-18T11:35:10Z-security-pass.md`
- All 53 regression tests passing; no regressions from security findings
- v0.3.1 production-readiness posture unchanged; findings feed v0.4.0 prioritization


## Team Update — 2026-05-19T08:18:00Z

**Purah** completed one-hop indirect stream /Length support with test coverage.
**Robbie** added regression coverage for indirect /Length cases.
**Decisions merged:** 6 inbox entries (roadmap, indirect-length cases, unsupported PDF categories).
**Archive status:** decisions.md at 64026 bytes; no entries older than 7 days.

