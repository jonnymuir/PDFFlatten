---
name: "downloads-only-real-form-hygiene"
description: "Keep real customer-form identifiers out of tracked repo surfaces while preserving manual Downloads-only validation"
domain: "repo-hygiene"
confidence: "high"
source: "earned"
---

## Context
Use this when a real customer PDF has been helpful during local validation, but its name, path, or business context should not live in repository docs, notes, logs, or fixtures.

## Patterns
- Search beyond product docs: include `.squad/` histories, decision inbox notes, logs, orchestration notes, and filenames.
- Replace real-form identifiers with generic descriptions or neutral placeholder names that do not imply a checked-in fixture.
- Keep repo tests, samples, and README examples pointed at sanitized or synthetic fixtures only.
- If manual real-file validation still matters, describe it as a private `~/Downloads` operator step rather than a GitHub-facing workflow.
- After cleanup, re-scan file contents and file paths for the sensitive token family, not just one exact filename.

## Examples
- `.squad/decisions.md` and agent histories can retain the technical lesson while renaming the real form to a generic Downloads-only description.
- `.squad/decisions/inbox/` and `.squad/log/` may need both content edits and filename renames.
- `README.md`, `samples/`, and `tests/` should stay on generic fixture names such as `GenericAcroFormFixture.pdf`.

## Anti-Patterns
- Scrubbing only tracked product docs while leaving the same identifier in squad logs or decision inbox files
- Replacing a sensitive real filename with another concrete-looking repo fixture name
- Turning a one-off local validation path into a documented repository acceptance workflow
