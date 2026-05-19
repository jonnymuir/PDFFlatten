---
name: "quicklook-real-pdf-smoke-check"
description: "Validate a real flattened PDF with macOS Quick Look before calling print output acceptable"
domain: "testing"
confidence: "medium"
source: "earned"
---

## Context
Use this when a real customer PDF has started flattening successfully and you need a practical acceptance check on macOS that goes beyond “the API returned success.” It is especially useful for print-oriented workflows where first-page appearance matters immediately.

## Patterns
- Run the same entry point the user will run, not a private helper.
- Keep the real output artifact at a stable user-facing path so manual follow-up is easy.
- Render both the original and flattened PDFs with `qlmanage -t -s 2048`.
- Compare the emitted PNG bytes or hashes; identical thumbnails are strong smoke-test evidence that visible output survived flattening.
- Pair the render check with the normal regression suite so the real-file win is not hiding unrelated breakage.

## Examples
- `samples/PDFFlatten.Sample/Program.cs` is the right sample-flow entry point for end-user validation.
- `tests/PDFFlatten.Tests/VisualEquivalenceTests.cs` shows the existing Quick Look renderer pattern already trusted in this repo.
- `artifacts/manual-real-form-validation/` held matching source/flattened thumbnails for a Downloads-only template form.

## Anti-Patterns
- Declaring victory from exit code alone
- Checking only PDF tokens without confirming the output still renders
- Introducing a broader rendering harness when Quick Look smoke evidence is already available
