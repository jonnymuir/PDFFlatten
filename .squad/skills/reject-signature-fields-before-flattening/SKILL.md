---
name: "reject-signature-fields-before-flattening"
description: "Fail closed on PDF signature widgets when flattening would preserve the mark but destroy verification semantics"
domain: "security"
confidence: "high"
source: "earned"
---

## Context
Use this when auditing or implementing AcroForm flattening. Digital signatures are not just annotations with pretty appearances; they are security controls whose verification semantics depend on the retained field/signature object graph.

## Patterns
- Reject `/FT /Sig` widgets before flattening their `/AP /N` appearance.
- Describe the rejection as a security boundary, not a generic unsupported oddity.
- Keep ordinary widget-appearance replay separate from signature handling; visual preservation alone is insufficient for signed documents.

## Examples
- `src/PDFFlatten/PdfFlattener.cs` rejects `/FT /Sig` in `CreatePlacement(...)` before any appearance reuse occurs.
- `tests/PDFFlatten.Tests/ParserHardeningTests.cs` uses a synthetic classic-xref signature widget fixture to prove the fail-closed behavior.

## Anti-Patterns
- Flattening a signature field because it has a valid `/AP /N` stream
- Claiming the output is "safe" or "equivalent" when signature verification has been stripped
- Treating signed-PDF handling as the same problem as text-field appearance preservation
