---
name: "narrow-needappearances-textfield-slice"
description: "Keep /NeedAppearances support scoped to an exact direct text-field shape instead of broad appearance rendering"
domain: "architecture"
confidence: "high"
source: "earned"
---

## Context
Use this when a real PDF appears to need only "a little" appearance generation to flatten, especially after parser or decryption work has already made the file look close to the current supported slice.

## Patterns
- Start by proving the file shape is genuinely narrow: direct `/FT /Tx` widgets, direct `/V`, `/DA`, `/DR`, direct `/Rect`, no `/AP`, direct page `/Annots`, direct page `/Resources`, zero rotation, and no non-text field types.
- Treat `/NeedAppearances` support as a new rendering lane, not a parser tweak. Keep it isolated from existing `/AP /N` replay logic and from unrelated parser widenings such as encryption or `/Length` handling.
- Approve only an exact-match lane for simple single-line text placement. Reject inherited operative attributes, multiline/comb/quadding behavior, rich text, non-text widgets, and generic "render whatever Acrobat would show" ambitions.
- Require widget-local font resolution and a tiny default-appearance grammar: one resolvable font alias plus simple gray/RGB color plus font-size extraction only. If rendering would need AcroForm inheritance, auto-fit, or border/background synthesis, reject it.
- Keep the public posture fail-closed: unsupported `/NeedAppearances` forms must still reject clearly rather than silently flatten "best effort."
- Require viewer-backed regression evidence before widening the supported slice claim.

## Examples
- The Downloads-only encrypted NeedAppearances form decrypts and then presents 12 direct `/Tx` widgets with self-contained `/V`, `/DA`, `/DR`, and no `/AP`, making it a plausible exact-match candidate but not evidence for broad `/NeedAppearances` support.
- The same Downloads-only encrypted-form values are all single-byte text (ASCII plus `£`) and point at widget-local `/He` from `/DR /Font`, which is the kind of bounded text lane you can review without approving generic Unicode or inherited-resource rendering.
- `src/PDFFlatten/PdfFlattener.cs` now carries the narrow implementation: only left-aligned single-line `/Tx` widgets with widget-local `/DA` + `/DR` + string `/V` can synthesize a simple text Form XObject when `/NeedAppearances` is true and `/AP` is missing.
- `tests/PDFFlatten.Tests/PdfFlattenerTests.cs` proves the synthetic lane, while `tests/PDFFlatten.Tests/ParserHardeningTests.cs` and `tests/PDFFlatten.Tests/UnsupportedPdfGuardTests.cs` keep unsupported `/NeedAppearances` shapes rejected.

## Anti-Patterns
- Reframing one real file as justification for general appearance rendering
- Accepting inherited `/FT`, `/DA`, `/DR`, or `/V` while also adding appearance synthesis
- Claiming support for all `/NeedAppearances` forms after validating only one narrow text-field template
