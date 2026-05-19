---
name: "one-hop-indirect-length-boundary-tests"
description: "Regression-test a narrow PDF parser widening for single-hop indirect stream /Length support"
domain: "testing"
confidence: "high"
source: "earned"
---

## Context
Use this when a classic-PDF parser is being widened by exactly one structure: allowing stream `/Length` to resolve through one indirect object. The goal is to prove the compatibility win without accidentally turning the parser into a general indirect-reference resolver.

## Patterns
- Keep fixtures tiny and local to the tests: one classic xref table, one widget, one page stream, one appearance stream.
- Add two success cases: the indirect length object comes after the stream object and before the stream object. This proves xref-driven lookup instead of object-order luck.
- Make the successful stream payload contain text like `endstream` inside the actual bytes so the test proves exact-length reads still work and the parser did not switch to naive marker scanning.
- Pair the happy path with fail-closed cases for chained indirection, cyclic/self-referential indirection, non-integer resolved values, and missing referenced objects.
- Assert rendered flattening consequences on success (`/AcroForm` removed, widget `/Annots` removed, `FldFlat*` XObject added), not just "no exception".

## Examples
- `tests/PDFFlatten.Tests/UnsupportedPdfGuardTests.cs` adds focused fixtures for single-hop indirect `/Length` support and reject cases.
- `src/PDFFlatten/Internals/PdfParser.cs` resolves one indirect `/Length` hop while keeping exact byte-count and `endstream` validation.
- `README.md` documents the supported slice as direct integers or a single indirect reference to an integer object.

## Anti-Patterns
- Using a broad real-world fixture as the only proof of support
- Expanding tests into a general indirect-object resolution matrix unrelated to `/Length`
- Accepting chained or cyclic references just because one-hop indirection was needed for compatibility
