# Team Decisions

## Impa Decision — GitHub Publish
- **Date:** 2026-05-14T22:26:30.112+01:00
- **Decision:** Treat `https://github.com/jonnymuir/PDFFlatten.git` as the canonical `origin` remote for this repository and publish the current `main` branch there.
- **Rationale:** The repository already has local history on `main`, the user explicitly created that GitHub destination, and publishing the current branch keeps the repo state simple and discoverable.
- **Notes:** Publishing can proceed non-interactively with `git push -u origin main`. Test verification remains limited until `BAPSL_P60_Populated.pdf` is restored at the repo root.
