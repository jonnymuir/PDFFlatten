---
name: "encrypted-pdf-boundary-triage"
description: "Decide whether a real encrypted PDF is a narrow support win or should stay a fail-closed boundary"
domain: "testing"
confidence: "medium"
source: "earned"
---

## Context
Use this when a real customer PDF fails with trailer `/Encrypt` and someone is tempted to treat it like a small parser compatibility gap. The goal is to characterize the encryption shape in practical library terms and prevent indirect-parser wins from being mis-sold as encryption support.

## Patterns
- Inspect the real file for trailer `/Encrypt` and the referenced encryption dictionary before discussing support scope.
- Translate the dictionary into practical terms (`/Filter /Standard`, `/V`, `/R`, key length, crypt filters present/absent) instead of speaking only in PDF-spec jargon.
- Check whether external tooling can open the file with an empty password, but do not confuse that with “no encryption”; it still requires a decryption path the library does not have.
- If the codebase intentionally rejects any `/Encrypt`, keep regression coverage fail-closed and mirror the real encryption shape in fixtures.
- Treat encryption as a category boundary, not a one-off parser tweak, unless the implementation explicitly adds decryption, password handling, and security review.

## Examples
- The Downloads-only encrypted NeedAppearances form is a one-page PDF 1.7 with trailer `/Encrypt 329 0 R` and Standard security `/V 2 /R 3 /Length 128`; PDFFlatten correctly rejects it before flattening.
- `tests/PDFFlatten.Tests/ParserHardeningTests.cs` now models encrypted rejection with a Standard-security `V2/R3/128-bit` fixture instead of only an older weak-encryption shape.
- `.squad/decisions/inbox/robbie-encrypted-pdf-validation.md` records the scope call: keep encrypted PDFs out of scope.

## Anti-Patterns
- Calling empty-password-openable PDFs “basically unencrypted”
- Framing encrypted-PDF support as the same class of work as indirect `/Length` parsing
- Claiming a real-file win without verifying the library can actually decrypt and flatten it
