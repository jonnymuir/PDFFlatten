### 2026-05-19T15:43:16.541+01:00: Purah release decision
**Decision:** Release the current parser/flattener expansion as `v0.5.0`.

**Why this version:** The package API remains stable, but the supported slice widens in three meaningful ways: empty-password RC4-128 input handling, single-hop indirect stream `/Length` support, and a narrow `/NeedAppearances` text rendering lane. That is bigger than a patch, but still clearly pre-1.0 because the library remains intentionally fail-closed outside its documented slice.

**Release hygiene:** Scrub tracked squad wording back to generic real-form language before tagging so Downloads-only local smoke checks do not leak into the public repository trail.
