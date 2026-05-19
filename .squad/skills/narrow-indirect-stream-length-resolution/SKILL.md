---
name: "narrow-indirect-stream-length-resolution"
description: "Support classic PDF indirect stream /Length values without widening into general lazy object resolution"
domain: "parser-design"
confidence: "high"
source: "earned"
---

## Context
Use this when a classic-xref PDF parser already handles exact-length stream reads, but real inputs use the common `/Length N 0 R` pattern. The goal is to improve compatibility without turning the parser into a general indirect-reference resolver.

## Patterns
- Resolve indirect `/Length` only inside stream parsing, never through a shared dictionary/object-resolution abstraction.
- Reuse the xref entry for the referenced object and parse exactly one target object header/value at that offset.
- Accept success only when the target object terminates cleanly and its value is a direct integer `PdfNumber`.
- Reject chained indirection, cycles, missing objects, non-integer targets, negative values, and values above the existing stream-size cap.
- Keep the downstream stream read logic unchanged: exact byte count first, then normal `endstream` validation. Do not add heuristic `endstream` scanning.

## Examples
- `src/PDFFlatten/Internals/PdfParser.cs` resolves stream `/Length` through a one-hop xref lookup and then reuses the existing `ReadBytes(length)` path.
- `tests/PDFFlatten.Tests/UnsupportedPdfGuardTests.cs` proves the supported one-hop case plus chained, cyclic, non-integer, and missing-target rejection.
- `tests/PDFFlatten.Tests/ParserHardeningTests.cs` keeps negative and oversized indirect `/Length` values on the same fail-closed cap path as direct lengths.

## Anti-Patterns
- Adding a recursive `ResolveValue()` helper that resolves arbitrary indirect references during parse.
- Scanning raw bytes for `endstream` because the parser no longer trusts exact lengths.
- Treating indirect `/Length` support as permission to widen other unsupported indirect structures.
