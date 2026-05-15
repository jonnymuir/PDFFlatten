# Changelog

All notable changes to this project will be documented in this file.

## [Unreleased]

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
