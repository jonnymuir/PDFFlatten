# Changelog

All notable changes to this project will be documented in this file.

## [Unreleased]

## [0.5.0] - 2026-05-19T15:43:16.541+01:00

### Added
- Narrow support for empty-password Standard-security RC4-128 AcroForm PDFs, with parser-local decryption and unencrypted rewritten output.
- Narrow support for single-hop indirect stream `/Length` integers and `/NeedAppearances`-driven single-line text appearance synthesis when widgets carry self-contained `/DA`, `/DR`, and `/V` data.
- Regression coverage for the encrypted supported slice, indirect `/Length` boundary cases, and the new text-appearance synthesis lane. Full suite now passes at 77 tests.

### Changed
- Scrubbed the remaining legacy real-form shorthand from tracked squad artifacts so release notes and logs stay generic before tagging a public release.

### Documentation
- Updated README supported-slice, rejection, and limitation guidance to reflect the narrow encrypted-input lane, indirect `/Length` support, and the bounded `/NeedAppearances` text path.

## [0.4.0] - 2026-05-18T18:32:01.569+01:00

### Changed
- Multi-targeted the NuGet package for `net462` and `netstandard2.0` so .NET Framework 4.6.2 consumers can load the direct `net462` assembly instead of relying on the old `netstandard` facade set that commonly triggers `System.ValueTuple` runtime binding problems in legacy web apps.

## [0.3.2] - 2026-05-18T13:05:30+01:00

### Changed
- Hardened the classic-parser rejection boundary with explicit caps for whole-input buffering, direct stream `/Length` reads, and FlateDecode appearance inspection so hostile PDFs fail closed instead of consuming unbounded memory.
- Normalized malformed numeric overflow and stream-length parsing failures to the documented `InvalidOperationException` contract instead of leaking raw runtime parsing exceptions.

### Added
- Regression coverage for documented VB.NET fallback handling, proving callers can log a warning, preserve the source PDF, and copy the original bytes when PDFFlatten rejects unsupported or malformed inputs.
- Focused parser hardening tests for digital-signature rejection, oversized inputs and streams, FlateDecode amplification limits, and malformed numeric edge cases. Full suite now passes at 61 tests.

### Documentation
- Clarified README supported-slice language around explicit in-memory safety caps and documented the rejection fallback pattern for .NET Framework / VB.NET consumers.

## [0.3.1] - 2026-05-15T19:37:58.260+01:00

### Changed
- Field-hierarchy boundary tightened: self-contained parent/child naming hierarchies remain supported, but widgets that inherit operative field attributes (`/FT`, `/DA`, `/DR`, `/V`) from parent field dictionaries now reject fail-closed with descriptive exceptions.

### Added
- Synthetic regression coverage for inherited field-attribute hierarchies, including `/FT`, `/DA`, `/DR`, `/V`, and fully-qualified partial-name reporting in rejection messages.
- A checked-in, non-sensitive producer fixture corpus (`reportlab`, `pdfrw`, `pypdf`) with provenance/sanitization metadata and regression tests covering both supported classic-xref inputs and intentional fail-closed rejections.

## [0.3.0] - 2026-05-15T08:29:23.433+01:00

### Added
- Parser hardening: 10 fail-closed guards reject unsupported PDF structures (/Prev, /Encrypt, /XRefStm, /ObjStm, /XFA, page /Rotate, appearance /Matrix, state-based appearances, indirect page /Annots, inherited page /Resources). All guards throw descriptive exceptions before writing output.
- Test coverage expansion: from 15 to 37 tests including ParserHardeningTests (9), UnsupportedPdfGuardTests (6), ExpandedCoverageTests (11), and existing regression cases. Renderability assertions verify font and resource resolution post-flattening.

### Changed
- Production-readiness posture: marked as production-ready for the documented classic-AcroForm supported slice; no longer best-effort arbitrary-PDF rewriting.

### Documentation
- Clarified the production-readiness boundary: PDFFlatten is production-ready for the supported classic-AcroForm slice only, not for arbitrary PDFs. README and API XML docs now spell out supported structures, rejection behavior, known limitations, not-recommended usage, and deployment guidance.
- Added concise references to follow-on enhancement areas: issue #5 (multi-producer fixture corpus), issue #6 (renderer/viewer equivalence), and issue #7 (inherited field-attribute hierarchies).

## [0.2.0] - 2026-05-15T06:54:58.103+01:00

### Changed
- Migrated the library implementation and console sample from VB.NET to C# while keeping the public `PdfFlattener` API and `netstandard2.0` package target intact.
- Moved repository validation to a `net10.0` sample/test/CI lane and updated solution and workflow packaging to use `PDFFlatten.csproj`.

### Documentation
- Updated the README and GitHub setup guidance to reflect the C# source layout, compatibility posture, and current tagged release flow.

## [0.1.0] - 2026-05-14T21:39:55.268+01:00

### Added
- First public `PdfFlattener` API
- Stream-to-stream and stream-returning flatten overloads
- Initial generic synthetic fixture coverage for AcroForm flattening

### Release posture
- Shipped as a constrained utility for a narrow classic-xref AcroForm slice, not as a general-purpose arbitrary-PDF flattening engine.
