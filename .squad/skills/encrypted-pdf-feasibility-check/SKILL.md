---
name: "encrypted-pdf-feasibility-check"
description: "Decide whether encrypted-PDF support is actually the binding blocker or just the first visible rejection"
domain: "review"
confidence: "high"
source: "earned"
---

## Context
Use this when a PDF rewriter currently rejects `/Encrypt` and someone wants to widen support for a specific real file. The goal is to separate “can we decrypt it?” from “would the decrypted file still fit the existing flatten/rewrite slice?” before approving scope expansion.

## Patterns
- Inspect the real `/Encrypt` dictionary first and classify the handler precisely (`/Filter`, `/V`, `/R`, `/Length`, crypt-filter keys).
- If feasible, decrypt the sample upstream with an external inspection tool and run the normal pipeline on the cleartext copy. This tells you whether encryption is the true blocker or only the first rejected feature.
- If the decrypted file still fails on missing `/AP`, `/NeedAppearances`, inherited resources, signatures, or other existing guards, keep the boundary narrow and say so explicitly.
- Treat encrypted-input support as a preprocessing capability only; it must not be used to imply sanitization of preserved JavaScript or other active content.

## Examples
- The Downloads-only encrypted NeedAppearances form uses `/Filter /Standard /V 2 /R 3 /Length 128` and decrypts with the empty password, but the cleartext file still fails in `src/PDFFlatten/PdfFlattener.cs` because its widgets lack `/AP /N` and rely on `/NeedAppearances`.
- `src/PDFFlatten/Internals/PdfParser.cs` still rejects `/Encrypt` at the trailer boundary, which is correct until a separate bounded decrypt step is explicitly approved.

## Anti-Patterns
- Approving “encrypted PDFs” broadly based on one file without checking the decrypted structure.
- Treating successful empty-password decryption as proof that the file now fits the flattening engine.
- Bundling password APIs, AES/crypt-filter support, and appearance generation into one compatibility change.
