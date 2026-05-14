---
last_updated: 2026-05-14T19:59:41.985Z
---

# Team Wisdom

Reusable patterns and heuristics learned through work. NOT transcripts — each entry is a distilled, actionable insight.

## Patterns

<!-- Append entries below. Format: **Pattern:** description. **Context:** when it applies. -->

**Pattern:** When you need an in-house first pass at AcroForm flattening, reuse each widget's normal appearance stream (`/AP /N`) as a page XObject and remove the widget annotation only after you have re-painted that appearance onto page content. **Context:** This gives a compatibility-friendly flattening path without taking a third-party PDF rendering dependency just to ship a stable public API.

- **Pattern:** Reuse normal widget appearance streams as page XObjects, then prune unreachable form objects during serialization. **Context:** First-pass AcroForm flattening when preserving existing visual appearance matters more than regenerating field graphics.
**Pattern:** When using real-world PDFs as fixtures, tolerate missing-but-unreachable indirect references during serialization rather than aborting the whole flatten pass. **Context:** Producer-generated PDFs can be structurally untidy even when their interactive content still renders correctly enough to flatten.
