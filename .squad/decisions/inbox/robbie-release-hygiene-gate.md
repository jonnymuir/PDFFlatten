# Robbie — Release hygiene gate

- **Date:** 2026-05-19T15:43:16.541+01:00
- **Decision:** Do not call legacy real-form reference cleanup complete until both the local tracked tree and GitHub code search on the pushed default branch are clean.
- **Why:** Team-note/history files can still leak old public references after the working tree looks clean locally, so the release gate must verify the public repo state rather than trust local grep alone.
- **Impact:** Release hygiene verdicts stay fail-closed until the branch is pushed and remote code search comes back clean.
