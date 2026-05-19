---
name: "sensitive-reference-scrub-audit"
description: "Audit tracked files for leaked real-form names and user-local test paths without confusing local smoke checks for repo contract"
domain: "testing"
confidence: "medium"
source: "earned"
---

## Context

Use this when a cleanup pass claims the repo no longer exposes sensitive real-world fixture names or user-local paths, but you need to verify the claim across docs, tests, sample text, and team notes.

## Patterns

- Search tracked content, not just shipping code, because team history and decision logs can still leak the old references.
- Check GitHub code search separately from the local tree before declaring a scrub complete; a clean worktree ahead of `origin/main` does not mean the public repo is clean yet.
- Separate the public repo contract from private operator workflow: generic CLI/docs are acceptable, committed user-local paths are not.
- Confirm the sample entry point still uses generic `input output` arguments rather than a baked-in local file path.
- Re-run the existing regression suite after audit writes so the repo state is still healthy.

## Anti-Patterns

- Declaring cleanup complete after checking only `src/` and `tests/`
- Treating a local smoke-test path as acceptable repository documentation
- Adding new tracked notes that repeat the sensitive reference you are trying to scrub
