---
name: "pdf-rewriter-security-audit"
description: "Audit a PDF rewrite/flatten pipeline for fail-closed boundaries, DoS risks, and sanitization misconceptions"
domain: "security"
confidence: "high"
source: "earned"
---

## Context
Use this when reviewing a PDF parser/rewriter that claims to flatten, rewrite, or normalize documents without a rendering engine. The main question is usually not code execution inside the library, but whether hostile PDFs can force unbounded work, break the documented rejection contract, or leave consumers with a false sense of sanitization.

## Patterns
- Separate **execution risk** from **preserved-content risk**. A flattener can be safe from code execution while still preserving `/OpenAction`, JavaScript, embedded files, or other active content.
- Check for unbounded whole-file buffering first. Stream-oriented public APIs often still copy the entire PDF into memory internally.
- Audit every `/Length`-driven read and every decompression path. If `CopyTo` or `new byte[length]` is driven by attacker-controlled metadata without a cap, treat it as a DoS finding.
- Compare real thrown exceptions against the documented contract. Raw `OverflowException`, `OutOfMemoryException`, or `ArgumentOutOfRangeException` from malformed input are security-relevant because callers often only catch the advertised rejection exceptions.
- In memory-buffered .NET PDF rewriters, cap whole-file reads, `/Length`-driven stream materialization, and decompression used only for inspection separately; map malformed numeric overflow/format failures to `InvalidOperationException`, but let genuine process-level OOM still fail loudly.
- Give extra credit to explicit fail-closed boundaries: rejecting encryption, incremental updates, xref streams, object streams, inherited resources, and unsupported appearance transforms materially shrinks the attack surface.

- Regression-test resource caps with a seekable synthetic stream for whole-file limits and a tiny compressed fixture that inflates past the decode cap; this proves DoS guardrails without checking giant binaries into the repo.

## Examples
- `src/PDFFlatten/PdfFlattener.cs` buffers the full input and inflates `FlateDecode` appearance streams during font-repair inspection.
- `src/PDFFlatten/Internals/PdfParser.cs` and `src/PDFFlatten/Internals/PdfReader.cs` parse attacker-controlled numeric tokens and stream lengths, so numeric overflow/bounds behavior must be reviewed as part of the public rejection contract.
- `src/PDFFlatten/Internals/PdfSerializer.cs` preserves all reachable non-form objects, which is correct for flattening but means the output is not a sanitizer-clean PDF by default.

## Anti-Patterns
- Declaring "no security issues" just because the library does not execute PDF JavaScript.
- Treating preserved dangerous content as irrelevant because the flattener did not create it.
- Ignoring memory amplification because the API surface accepts `Stream` instead of `byte[]`.
- Trusting exception XML docs without probing malformed numeric and stream-length cases.
