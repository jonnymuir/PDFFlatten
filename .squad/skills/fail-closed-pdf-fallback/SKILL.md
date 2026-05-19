---
name: "fail-closed-pdf-fallback"
description: "Document honest consumer fallback patterns for PDFs rejected outside PDFFlatten's supported slice"
domain: "error-handling"
confidence: "high"
source: "earned"
---

## Context
Use this when README or sample docs need to show how callers should react when PDFFlatten rejects an input PDF. The goal is to keep the supported-slice story honest and give consumers a practical fallback that preserves the original file.

## Patterns
- Catch `NotSupportedException` for known unsupported structures and `InvalidOperationException` for malformed or incomplete inputs.
- Describe both exception types as rejection signals, not partial-success conditions.
- Log a warning, keep the source PDF unchanged, and copy the original input to the chosen fallback output path when the workflow still needs an output artifact.
- Keep parser capability separate from product behavior: an unsupported PDF may be impossible to flatten with the current engine while still being safe to preserve via pass-through fallback.
- Keep automatic pass-through inside the library limited to true no-op cases (for example no AcroForm/widgets). For unsupported interactive PDFs, make pass-through an explicit caller or sample-app policy, not an ambiguous silent success.
- Keep examples explicit about PDFFlatten's constrained supported slice; do not imply best-effort flattening.

## Examples
- `README.md` VB.NET quick-start fallback snippet copies `input.pdf` to `flattened-or-original.pdf` after writing a warning to stderr.
- `src/PDFFlatten/PdfFlattener.cs` XML docs define `NotSupportedException` and `InvalidOperationException` as the honest rejection contract.
- `tests/PDFFlatten.Tests/DocumentedFallbackTests.cs` proves the fallback flow end-to-end by asserting warning logging plus byte-for-byte preservation of the source and fallback output for both unsupported and malformed inputs.

## Anti-Patterns
- Catching every exception and claiming the PDF was "flattened as much as possible"
- Overwriting the only copy of the source PDF during rejection handling
- Documenting only `NotSupportedException` when malformed-input rejection is also part of the public contract
