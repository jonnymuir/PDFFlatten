---
name: "narrow-empty-password-pdf-encryption"
description: "Add the smallest safe encrypted-PDF support slice to a classic in-memory PDF rewriter"
domain: "compatibility"
confidence: "high"
source: "earned"
---

## Context
Use this when a classic xref-table PDF rewriter needs to handle one real encrypted form without turning into a general-purpose crypto/PDF engine. The safe question is not “can we support encryption”, but “can we support one exact encryption shape locally and keep every other shape rejected?”

## Patterns
- Inspect the real file first and identify the exact `/Encrypt` shape before changing code.
- Keep encryption support parser-local: validate the trailer `/Encrypt` reference, parse the encryption dictionary directly from xref offsets, and derive the file key before object parsing.
- For a narrow, compatibility-friendly slice, allow only Standard security RC4-128 (`/Filter /Standard`, `/V 2`, `/R 3`, `/Length 128`) with no `/CF`, `/StmF`, `/StrF`, `/EFF`, `/EncryptMetadata`, or password prompt surface.
- Validate the empty user password explicitly from `/U`; do not silently assume other passwords or owner-password unlock flows.
- Decrypt strings and streams per object key, skip decrypting the encryption dictionary object itself, and remove trailer `/Encrypt` before serializing flattened output so the rewritten file is honestly unencrypted.
- Do not trust literal strings to stay plain after encryption: Acrobat-style files can encode encrypted bytes with octal escapes, so parser string decoding must handle `\\ddd` escapes before decryption or widget-local `/DA` and other strings will be corrupted.
- After adding decryption, re-check the original non-crypto supported-slice gates. Real encrypted files often still fail later on `/NeedAppearances`-only widgets or other semantics that should remain fail-closed.
- Accept decrypted PDF strings in either literal or hex-string form when the downstream supported slice only cares about bytes; encrypted fixtures can surface widget-local `/DA` that way without implying broader appearance-generation support.

## Examples
- `src/PDFFlatten/Internals/PdfStandardEncryption.cs` implements the RC4-128 empty-password gate plus object-wise decryption.
- `src/PDFFlatten/Internals/PdfParser.cs` creates the encryption context before parsing objects and strips `/Encrypt` from the trailer for rewritten output.
- `tests/PDFFlatten.Tests/UnsupportedPdfGuardTests.cs` includes a positive empty-password RC4 fixture, while `ParserHardeningTests.cs` still proves unsupported encryption shapes reject.
- `src/PDFFlatten/PdfFlattener.cs` accepts decrypted widget `/DA` values from either PDF string form so encrypted `/NeedAppearances` fixtures can stay inside the same narrow text-field slice.

## Anti-Patterns
- Treating “encrypted PDF” as one feature bucket and accidentally accepting AES, crypt filters, owner-password-only flows, or password prompts.
- Claiming success on a decrypted file that still lacks `/AP /N` and really needs appearance generation.
- Leaving trailer `/Encrypt` in place after serializing plaintext rewritten objects.
