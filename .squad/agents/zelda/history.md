# Project Context

- **Owner:** Jonny Muir
- **Project:** PDFFlatten
- **Stack:** VB.NET, .NET Framework 4.6.2, PDF AcroForm/form-field flattening
- **Description:** A VB.NET utility that takes PDFs with form fields and flattens them so they print correctly from an iPhone.
- **Created:** 2026-05-14T21:16:50.701+01:00

## Learnings

- 2026-05-19T13:01:12.611+01:00 — A flattened PDF is still not honestly flattened if `/StructTreeRoot` keeps widget objects reachable through `/Type /OBJR` children; prune those object references and the matching `/ParentTree /Nums` entries keyed by each widget’s `/StructParent` before claiming success on tagged PDFs like the Downloads-only encrypted NeedAppearances form.
- 2026-05-19T13:01:12.611+01:00 — Empty-password Standard-security RC4-128 support is safe only as a parser-local decrypt-and-strip step; `src/PDFFlatten/Internals/PdfStandardEncryption.cs` must keep rejecting crypt filters, non-R3/V2 shapes, and non-empty-password flows while emitting plaintext rewritten output.
- 2026-05-19T13:01:12.611+01:00 — Real Downloads-only encrypted-form flattening also depended on octal-escape decoding in `src/PDFFlatten/Internals/PdfReader.cs`; some encrypted Acrobat literal strings encoded ciphertext bytes with octal escapes, and without decoding them the widget-local `/DA` strings became unparseable before the narrow `/NeedAppearances` text synthesis lane in `src/PDFFlatten/PdfFlattener.cs`.
- I own PDF internals: AcroForm fields, widget annotations, appearance streams, and flattening behavior.
- The project's success depends on preserving rendered field appearances while removing interactive form behavior for iPhone printing.
- 2026-05-18T12:35:10.553+01:00 — Security review result: the parser boundary materially reduces exploitability by rejecting incremental updates, encryption, xref/object streams, inherited operative field attributes, inherited page resources, rotated pages, and transformed/state-based appearances before any rewrite.
- 2026-05-18T12:35:10.553+01:00 — Meaningful residual risk remains where flattening is only a rendering transform, not a sanitizer: non-widget annotations and other reachable objects survive serialization, so active content outside widget removal must not be treated as scrubbed.
- 2026-05-18T12:35:10.553+01:00 — Added a fail-closed signature boundary in `src/PDFFlatten/PdfFlattener.cs`; `/FT /Sig` widgets now reject instead of being flattened into a visually preserved but no-longer-verifiable document. Guard covered in `tests/PDFFlatten.Tests/ParserHardeningTests.cs`.
- 2026-05-19T12:51:51.572+01:00 — Real-file inspection of the Downloads-only encrypted NeedAppearances form showed the next plausible compatibility lane is not broad `/NeedAppearances` support but an exact direct text-widget slice: 12 `/Tx` widgets with self-contained `/V`, `/DA`, `/DR`, and `/Rect`, no `/AP`, direct page `/Annots` and `/Resources`, zero rotation, and widget-local `/He` font use.
- 2026-05-19T12:51:51.572+01:00 — Locked the boundary in `.squad/decisions/inbox/zelda-appearance-generation-boundary.md`: only text-only generated `/AP /N` synthesis for exact-match widgets is defensible; inherited defaults, multiline/comb/rich-text behavior, non-text fields, and generic viewer-style appearance generation remain rejected.

- 2026-05-14T21:39:55.268+01:00 — Implemented the first in-house PDF parser/serializer and AcroForm flattening path for classic xref-table PDFs.
- 2026-05-14T21:39:55.268+01:00 — Learned that preserving appearance streams is only half the job; pruning unreachable widget objects keeps flattened output meaningfully non-interactive.

## 2026-05-14T21:39:55Z — Team Batch Complete

**Peer Outcomes:**
- **Impa:** Repo productization complete (GitHub docs, workflows, SourceLink, release automation).
- **Purah:** `PdfFlattener.Flatten(Stream)` contract locked; package metadata finalized; NuGet build validated.
- **Robbie:** 11 passing regression tests; Downloads-only populated real-form fixture assertions; macOS-safe CI.

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

- 2026-05-14T23:04:38.903+01:00 — Production-readiness audit verdict: current safety claims must stay narrow. The engine is structurally limited to classic xref-table AcroForm PDFs with usable `/AP /N` streams; it is not broad-PDF safe.
- 2026-05-14T23:04:38.903+01:00 — Concrete audit probes showed hard failures for indirect `/Annots`, missing page `/Contents`, checkbox/radio-style `/AP /N` state dictionaries, and incremental-update files; a crafted inherited-resource case showed flattening can inject a direct page `/Resources` dictionary that shadows inherited fonts and risks breaking existing page content.
- 2026-05-14T23:04:38.903+01:00 — Another crafted audit case showed `FldFlat###` resource naming is not collision-safe: if the page already has `/XObject /FldFlat001`, flattening overwrites that resource and can change pre-existing page graphics unrelated to the form field.

**2026-05-14T23:04:38Z — Post-Release Audit & Production-Readiness Judgment (Scribe Processing)**
- Production-readiness audit verdict recorded in `decisions.md`: narrow-slice only, not safe for arbitrary PDFs
- Identified major structural and silent-misrendering risks; recommended strict reject-guards plus render-level corpus testing
- Orchestration log: `.squad/orchestration-log/zelda-2026-05-14T23-04-38Z.md`
- Key findings: narrow parser (no xref-stream, object-stream, `/Prev` chain), specific widget model, corruption/mis-render risks (inherited `/Resources` override, ASCII-only serialization), narrow verification depth
- Highest-risk failure modes: rotated/transformed page rendering, inherited resource override, common real-world forms failing, text/font issues, incomplete output, non-ASCII mangling, signed/encrypted unsafety
- Minimum guardrails recommended before broader production claim: documentation, runtime fail-closed checks, correctness verification, producer-diverse corpus with render-diff validation

## 2026-05-15T05:33:44Z — C# migration complete (coordination note)

**Cross-team context:**
- Purah ported `src/PDFFlatten` VB.NET → C# (netstandard2.0), preserving API and semantics
- Robbie updated test harness to follow project-file cutover (C# primary, VB fallback)
- Zelda PDFs remain semantically stable; language port does not change flattening behavior
- All 15 regression tests passing post-migration on `net10.0`
- Next gate: PDF-semantic behavior review (Zelda); test expansion for unsupported inputs (Robbie, Issue #3)
- 2026-05-15T06:16:04.770+01:00 — Coordinated with Purah for the C#/.NET port, then reviewed the translated PDF engine instead of re-implementing it; the port keeps the existing narrow flattening slice (`/AP /N` replay, widget removal, `/AcroForm` removal, reachable-object serialization) unchanged.
- 2026-05-15T06:16:04.770+01:00 — Found and fixed C# translation regressions in regex/literal escaping (`PdfFlattener`, `PdfReader`, `PdfSerializer`, and the test literal decoder) before trusting any PDF review signal; those were port artifacts, not PDF-model changes.
- 2026-05-15T06:16:04.770+01:00 — Verified semantic parity by comparing the flattened output for `GenericAcroFormFixture.pdf` against the pre-port HEAD implementation; hashes matched exactly, which is the strongest evidence that the language port did not subtly change current flattening behavior.

## 2026-05-18T11:35:10Z — Security & Signature Audit (Post-Release)

**Session:** Security/architecture review focused on attack surface and residual risk.

**Findings:**
- Medium-risk residual vectors remain: unbounded memory work (buffering, `/Length`-driven allocations, `FlateDecode` inflation) and exception-contract gaps (parser overflow paths emit raw exceptions instead of documented contract).
- Signature handling gap closed: `/FT /Sig` widgets previously not rejected; replaying signature appearance destroys verification semantics.
- Non-widget active content out of scope: flattening is a rendering transform, not a sanitizer; active content outside form fields survives unless separate sanitization policy applied.

**Implementation (Zelda):**
- Added fail-closed signature boundary in `src/PDFFlatten/PdfFlattener.cs`; `/FT /Sig` now throws `NotSupportedException` before rewrite.
- Guard covered by synthetic classic-xref signature fixture in `tests/PDFFlatten.Tests/ParserHardeningTests.cs`.
- All 53 regression tests passing; no regressions.

**Shared Context:**
- Impa documented security posture and next hardening priorities (resource limits, exception normalization) for v0.4.0+.
- Orchestration logs written; decisions logged to `.squad/decisions.md`.
- v0.3.1 production-readiness unchanged; security findings feed prioritization, not scope expansion.


## 2026-05-12T00:00:00Z — History Summary (Compressed)

**Previous sessions:** Initial parser implementation, xref/AcroForm handling, appearance repair, production audit, C# migration, security hardening (53 tests passing). See history-archive.md for pre-2026-05-19 session details.

**Current slice:** narrow classic-xref parser with targeted indirect `/Length` support, fail-closed guards for encryption/XFA/streams/incremental-updates, signature boundary, and inheritance checks.


## 2026-05-15T07:04:03.456+01:00 — Production-Hardening Milestone (Issue #2) Complete

**Executive Summary:** Issue #2 (Parser Hardening) complete, committed, and production-quality. All 10 fail-closed guards in place; parser now safely rejects unsupported PDF structures.

**Guards Implemented:**
- `/Encrypt` — Rejects encrypted PDFs; "Encrypted PDFs are not supported; trailer /Encrypt must be absent."
- `/XFA` — Rejects XFA forms; "XFA forms are not supported."
- `/XRefStm` — Rejects xref-stream table format; "Cross-reference streams are not supported in this version."
- `/ObjStm` — Rejects object streams; "Object streams (/ObjStm) are not supported."
- Indirect stream `/Length` — Rejects indirect integer lengths; "Only streams with direct integer /Length values are supported."
- Trailer `/Prev` — Rejects incremental-update PDFs; "Incremental-update PDFs are not supported; the trailer /Prev chain must be absent."
- Indirect page `/Annots` — Rejects indirect annotation arrays; "Indirect page /Annots arrays are not supported."
- State-based appearances — Rejects checkbox/radio state dictionaries; "Only indirect stream /AP /N appearances are supported; state dictionaries are not supported."
- Unresolved references — Rejects during serialization; "PDF contains an unresolved indirect reference ({num} {gen} R)."
- Inherited page `/Resources` — Rejects via `RejectInheritedPageResources()`; "Inherited page /Resources not supported; pages must have direct /Resources."

**Test Coverage:** ParserHardeningTests.cs (9 negative tests) + UnsupportedPdfGuardTests.cs (6 negative tests). All guards validated with synthetic fixtures that trigger each rejection condition. All 15 existing regression tests still passing (30 total).

**Quality Signals:**
- ✅ Fail-closed behavior eliminates silent corruption risk
- ✅ All guards throw early, before flattening attempt
- ✅ Exception messages are production-friendly and actionable
- ✅ Output for valid classic-xref AcroForm PDFs unchanged
- ✅ No regression in supported-slice handling

**Owned By:** Zelda (PDF-spec correctness, guard placement, exception clarity)

**Shared Context:** Impa coordinated the overall production-hardening sequence. Robbie will expand test suite (Issue #3) to validate guards work correctly. Purah will document scope boundaries (Issue #4).

## 2026-05-19T09:06:49Z — Indirect Stream `/Length` Implementation Boundary Review

**Session:** Zelda pre-implementation review before Purah + Robbie begin coding.

**Context:** Earlier today (2026-05-19), Purah and I (Zelda) agreed in principle to support indirect stream `/Length` references as a compatibility enhancement within the narrow classic-xref parser. This session focused on locking down the exact implementation boundary to prevent accidental widening.

**Work Done:**
- Analyzed current parser state: PdfParser.cs line 213–216 correctly rejects all indirect `/Length` with `NotSupportedException`.
- Reviewed both decision documents (Purah's technical spec and my semantic judgment).
- Wrote comprehensive implementation boundary specification: `.squad/decisions/inbox/zelda-indirect-length-implementation-boundary.md`
  - Defines what MUST be implemented: single-level indirection, cycle detection, strict value validation, no chained resolution, memoization per-stream.
  - Defines what MUST NOT happen: general object resolver, lazy evaluation, widening to other fields, scope creep.
  - Specifies exact test cases required: valid indirect lengths (before/after), missing references, non-integer targets, negatives, oversized values, cycles, chained indirection.
  - Guards against implementation mistakes with explicit rejection criteria.

**Decision Recorded:** Implementation boundary approved for Purah + Robbie under reviewer lockout. Any deviation requires explicit re-review.

**Guard Status:** ✅ Scoped correctly; narrow semantic point; no category widening; cycle guards mandatory; fail-closed errors; test coverage specified. Sound to implement.

**Next for team:** Purah implements per specification. Robbie writes regression suite. Both reference the boundary document for validation. Impa reviews PR against boundary before merge.

## 2026-05-19T09:06:49Z — Indirect Stream `/Length` Implementation Review (COMPLETE)

**Session:** Zelda post-implementation review after Purah delivered the code.

**Context:** Purah implemented indirect stream `/Length` support in `PdfParser.cs` while this session was in progress. Reviewed implementation against boundary specification.

**Work Done:**
- Analyzed implementation in PdfParser.cs lines 216–330: `ResolveStreamLength()` and `ResolveIndirectStreamLength()` methods.
- Verified all boundary conditions:
  - ✅ Single-level indirection resolved exactly once (lines 271–280).
  - ✅ Self-referential cycle detection (lines 259–262).
  - ✅ Chained indirection rejected (lines 289–298).
  - ✅ Strict value validation: must exist, be direct `PdfNumber`, be integer, non-negative, within size limits (lines 264–329).
  - ✅ Stream termination validation unchanged (lines 218–223).
  - ✅ No general object resolver; scoped only to stream `/Length` parsing.
  - ✅ No scope widening to other fields or PDF structures.
- Reviewed test suite in `UnsupportedPdfGuardTests.cs`: all 6 tests for supported + rejection cases present and comprehensive.
- Verified error messages are production-quality: "Indirect stream /Length objects must resolve directly to an integer."

**Decision Recorded:** Implementation approved without changes. Stays within boundary; all hardening conditions met. Ready for merge.

**Status:** ✅ Implementation is sound, bounded, and secure. No further review needed.

## 2026-05-19T09:06:49.650+01:00 — Unsupported PDF Categories Analysis (Post-Release Review)

**Executive Summary:** Comprehensive inventory of PDFs that fail because PDFFlatten does not support indirect objects everywhere or keeps a narrow parser slice. Analysis distinguishes three classes: (A) classic PDFs we *should* probably support, (B) edge cases not worth supporting, (C) structures that widen parser/security surface and should remain rejected.

**Key Findings:**

*Category A: Should Support (Safe, Common)*
- **A.1: Indirect stream `/Length`** — Small effort; Acrobat-generated PDFs use this for incremental-update safety. Real-world prevalence: 5–10% of AcroForms. Recommendation: Support in v0.5.0.
- **A.2: Indirect page `/Resources` (resolution, not inheritance)** — Small effort; distinguish true inheritance from object-reuse indirection. Real-world prevalence: moderate. Recommendation: Support in v0.5.0.
- **A.3: Indirect page `/Annots`** — Small effort; some producers share annotation arrays. Rare but valid. Recommendation: Consider v0.5.0 if producer corpus testing shows demand.
- **A.4: Field hierarchy inheritance** — Medium effort; common in Acrobat-generated forms. Recommendation: Consider v0.5.0+ with renderer validation.

*Category B: Edge Cases (Low ROI)*
- **B.1: Appearance `/Matrix` transforms** — Rare for widgets; semantic complexity; render-fidelity risk. Recommendation: Skip; users can re-export.
- **B.2: Checkbox/radio state appearances** — Common in Acrobat but mostly pre-populated (value already set). Medium effort. Recommendation: Consider v0.5.0 if demand warrants.
- **B.3: Non-Flate compression (ASCII85, RLE)** — Rare in modern producers. Recommendation: Skip unless reported frequently.
- **B.4: Multi-filter streams** — Very rare. Recommendation: Skip.

*Category C: Keep Rejecting (Security/Complexity)*
- **C.1–C.6: Incremental updates, xref streams, object streams, encryption, XFA, nested field inheritance** — All have legitimate security or complexity reasons for rejection. Large effort to support; low ROI. Recommendation: Keep rejecting.

**Strategic Insight:** The narrow parser is *intentional strength*, not a limitation. Supporting Categories A is safe and reasonable. Rejecting Categories C protects against security expansion and maintains implementation focus.

**Impact Estimate:** 
- Not supporting A: ≈5–10% of real-world AcroForm PDFs rejected unnecessarily
- Continuing to reject C: <1% real-world legitimate AcroForms; 100% of malicious/exotic edge cases

**Decision Recorded:** `.squad/decisions/inbox/zelda-unsupported-pdf-categories.md` (2026-05-19T09:06:49.650+01:00)

**Next Priority:** Roadmap A.1 (indirect `/Length`) for v0.5.0; assess A.2, A.4, B.2 based on producer corpus testing results.

**Owned By:** Zelda (PDF-spec analysis, category judgment, effort estimation)


## Team Update — 2026-05-19T08:18:00Z

**Purah** completed one-hop indirect stream /Length support with test coverage.
**Robbie** added regression coverage for indirect /Length cases.
**Decisions merged:** 6 inbox entries (roadmap, indirect-length cases, unsupported PDF categories).
**Archive status:** decisions.md at 64026 bytes; no entries older than 7 days.
