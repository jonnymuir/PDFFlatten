# Changelog

All notable changes to this project will be documented in this file.

## [Unreleased]

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
