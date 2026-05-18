# 2026-05-18 — Release-facing metadata for direct net462 compatibility ship

- **Decision:** Prepare the `net462;netstandard2.0` compatibility update for release as **v0.4.0** and align release-facing metadata with that consumer-visible packaging change.
- **Why:** Impa's semver call is available (`v0.4.0`), and the new direct `net462` asset materially changes package selection for .NET Framework 4.6.2 consumers even though the public API stays the same.
- **Release metadata:** Stamp `CHANGELOG.md` with the `0.4.0` release entry, update `Directory.Build.props` `VersionPrefix` to `0.4.0`, and replace stale package release notes with the net462/netstandard2.0 compatibility summary so NuGet consumers see the actual runtime story.
- **Compatibility note:** The goal is narrower than "all legacy Framework pain disappears"—the package should stop causing the specific `netstandard` facade/`System.ValueTuple` churn that old ASP.NET apps hit when only a `netstandard2.0` asset is available.
