---
name: "structure-tree-widget-pruning"
description: "Keep flattened PDFs from retaining reachable widget objects through logical-structure references"
domain: "architecture"
confidence: "high"
source: "earned"
---

## Context
Use this when a PDF appears flattened because `/AcroForm` and page `/Annots` are gone, but real outputs still carry `/Subtype /Widget` objects.

## Patterns
- Check for `/StructTreeRoot`, `/ParentTree`, and `/Type /OBJR` references before approving “widgets removed.”
- Treat widget pruning as a reachability problem, not just an annotation-array problem: `PdfSerializer` will keep any widget that remains referenced from logical structure.
- For narrow supported slices, prefer one of two honest outcomes: prune the logical-structure references that target flattened widgets, or reject that file shape fail-closed.
- When pruning tagged-PDF widget references, drop both the `/OBJR` child that points at the widget and the matching `/ParentTree /Nums` entry keyed by that widget’s `/StructParent`; removing only one side leaves the logical structure internally inconsistent.
- Do not claim success on a real corpus file until the rewritten bytes no longer keep widget objects reachable through non-page references.

## Examples
- The Downloads-only encrypted NeedAppearances form still serialized 12 `/Subtype /Widget` objects after flattening because `/OBJR` nodes in the structure tree referenced them.
- The honest fix for that Downloads-only encrypted NeedAppearances form was to prune `/OBJR -> /Obj` references to the 12 flattened widgets and remove `/ParentTree /Nums` entries `1..12`, while leaving the rest of the tagged structure intact.
- `src/PDFFlatten/PdfFlattener.cs` removes `/AcroForm` and page `/Annots`, while `src/PDFFlatten/Internals/PdfSerializer.cs` preserves all remaining reachable objects.

## Anti-Patterns
- Equating “page `/Annots` removed” with “widgets removed”
- Approving a flattening lane against one real file without checking residual widget reachability in the output bytes
