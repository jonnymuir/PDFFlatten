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
- When triaging a real no-`/AP` `/NeedAppearances` form, inventory every widget’s `/Ff` bits and `/Q` value before proposing support; the exact approved lane is narrower than “text fields in general,” so centered quadding, mixed flag sets, or multiline-right-aligned combinations still push the file outside the supported slice.
- Treat `/NeedAppearances` support as a new rendering lane, not a parser tweak. Keep it isolated from existing `/AP /N` replay logic and from unrelated parser widenings such as encryption or `/Length` handling.
- Approve only an exact-match lane for simple single-line text placement. Reject inherited operative attributes, multiline/comb/quadding behavior, rich text, non-text widgets, and generic "render whatever Acrobat would show" ambitions.
- If a real file only needs one extra layout behavior, widen one notch at a time and pin the new edge with rejects on both sides. For the current lane, that means right-aligned single-line text and left-aligned multiline text are acceptable, but centered quadding and broader flag combinations still fail closed.
- When you add measured layout behavior, keep the font slice explicit. The current widened lane depends on the same widget-local `/DA` + `/DR` contract plus a narrow Helvetica-family Type1/WinAnsi measurement path rather than generic font metrics.
- Require widget-local font resolution and a tiny default-appearance grammar: one resolvable font alias plus simple gray/RGB color plus font-size extraction only. If rendering would need AcroForm inheritance, auto-fit, or border/background synthesis, reject it.
- Keep the public posture fail-closed: unsupported `/NeedAppearances` forms must still reject clearly rather than silently flatten "best effort."
- Require viewer-backed regression evidence before widening the supported slice claim.

## Examples
- The Downloads-only encrypted NeedAppearances form decrypts and then presents 12 direct `/Tx` widgets with self-contained `/V`, `/DA`, `/DR`, and no `/AP`, making it a plausible exact-match candidate but not evidence for broad `/NeedAppearances` support.
- A separate Downloads-only two-page encrypted NeedAppearances form looks close at first glance but immediately falls outside the slice because one widget is multiline (`/Ff 4096`) and five amount widgets are right-aligned (`/Q 2`); that is layout rendering work, not a tiny parser win.
- The same Downloads-only encrypted-form values are all single-byte text (ASCII plus `£`) and point at widget-local `/He` from `/DR /Font`, which is the kind of bounded text lane you can review without approving generic Unicode or inherited-resource rendering.
- `src/PDFFlatten/PdfFlattener.cs` now carries the narrow implementation: widget-local `/DA` + `/DR` + string `/V` can synthesize simple left-aligned single-line text, right-aligned single-line text, and left-aligned multiline text when `/NeedAppearances` is true and `/AP` is missing.
- `tests/PDFFlatten.Tests/PdfFlattenerTests.cs` proves the synthetic lane, while `tests/PDFFlatten.Tests/ParserHardeningTests.cs` and `tests/PDFFlatten.Tests/UnsupportedPdfGuardTests.cs` keep unsupported `/NeedAppearances` shapes rejected.
- After widening for the Downloads-only multiline-plus-amounts form shape, `tests/PDFFlatten.Tests/PdfFlattenerTests.cs` also proves right-aligned single-line widgets, left-aligned multiline widgets, wrapped multiline text, and a mixed six-widget supported document, while `tests/PDFFlatten.Tests/UnsupportedPdfGuardTests.cs` still rejects centered quadding, password-style flags, and multiline-right-aligned combinations.

## Anti-Patterns
- Reframing one real file as justification for general appearance rendering
- Accepting inherited `/FT`, `/DA`, `/DR`, or `/V` while also adding appearance synthesis
- Treating one successful multiline or right-aligned fix as approval for centered text, arbitrary font metrics, or generic text reflow
- Claiming support for all `/NeedAppearances` forms after validating only one narrow text-field template
