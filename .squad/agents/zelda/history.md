# Project Context

- **Owner:** Jonny Muir
- **Project:** PDFFlatten
- **Stack:** VB.NET, .NET Framework 4.6.2, PDF AcroForm/form-field flattening
- **Description:** A VB.NET utility that takes PDFs with form fields and flattens them so they print correctly from an iPhone.
- **Created:** 2026-05-14T21:16:50.701+01:00

## Learnings

- I own PDF internals: AcroForm fields, widget annotations, appearance streams, and flattening behavior.
- The project's success depends on preserving rendered field appearances while removing interactive form behavior for iPhone printing.

- 2026-05-14T21:39:55.268+01:00 — Implemented the first in-house PDF parser/serializer and AcroForm flattening path for classic xref-table PDFs.
- 2026-05-14T21:39:55.268+01:00 — Learned that preserving appearance streams is only half the job; pruning unreachable widget objects keeps flattened output meaningfully non-interactive.

## 2026-05-14T21:39:55Z — Team Batch Complete

**Peer Outcomes:**
- **Impa:** Repo productization complete (GitHub docs, workflows, SourceLink, release automation).
- **Purah:** `PdfFlattener.Flatten(Stream)` contract locked; package metadata finalized; NuGet build validated.
- **Robbie:** 11 passing regression tests; `BAPSL_P60_Populated.pdf` fixture assertions; macOS-safe CI.

**Zelda's Role in Batch:**
Built the engine that Purah's API wraps. Implemented xref table parsing, widget appearance reuse strategy, AcroForm removal, orphan pruning. This work is now end-to-end validated by Robbie's fixture tests.

**Next for Zelda:**
Core path is stable. Future enhancements can extend (incremental stream parsing, field value rendering, edge case PDFs) without breaking the working implementation. Edge cases and performance are secondary priorities.

- 2026-05-14T22:30:41.645+01:00 — Replaced the missing sensitive fixture with `GenericAcroFormFixture.pdf`, a synthetic one-page AcroForm PDF whose widgets embed `/T`, `/V`, and `/AP /N` data directly so current characterization and flattening tests stay meaningful.
- 2026-05-14T22:30:41.645+01:00 — The generic fixture intentionally stays within the current supported parser slice: classic xref table PDFs, direct page annotation arrays, and reusable normal appearance streams for each widget.

### 2026-05-14T22:33:44.877+01:00 — Fixture replacement & tests validated

**Session:** Scribe orchestration for Zelda & Purah spawn manifest

**Work completed:**
- Fixture replacement work (`GenericAcroFormFixture.pdf`) and test updates verified to pass regression suite cleanly (13/13 tests).
- Decision recorded in `.squad/decisions/inbox/zelda-generic-fixture.md` (merged into decisions.md by Scribe).
- Orchestration log: `.squad/orchestration-log/zelda-2026-05-14.log`.

**Status:** All fixture-replacement work complete and validated. Library engine remains stable.

- 2026-05-14T22:36:43.725+01:00 — Diagnosed the real-value-loss case as broken text appearance resources, not missing `/V` data: the populated `/AP /N` streams draw `/Helv`, but their `/Resources /Font` entry resolves to invalid object `253 0 R`, so appearance reuse flattened the page without a resolvable text font.
- 2026-05-14T22:36:43.725+01:00 — Hardened flattening for text widgets by repairing unresolved appearance font aliases from widget `/DA` + `/DR` (and standard Acrobat aliases when needed), while keeping the original appearance content and placement intact; added a synthetic compressed regression that exercises the same broken-resource shape.

## 2026-05-14T21:48:37Z — Appearance Resource Repair Complete

**Team Outcome:**
- **Robbie** added `AppearanceResourceRegressionTests` to guard font renderability in flattened output.
- **Scribe** merged decisions and logged orchestration for the session.

**Session Result:**
All 15 regression tests passing. The flattening engine now repairs broken appearance font resources before reusing appearances, ensuring visible text in flattened PDFs even when source widget appearances have orphaned font references. Real-world PDF validation (`/Users/jonnymuir/Downloads/flattened.pdf`) confirms field values render correctly after flattening.
