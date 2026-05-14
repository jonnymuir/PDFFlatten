# Squad Decisions

## Active Decisions

- 2026-05-14T21:16:50.701+01:00 — The squad uses Zelda-derived cast names for project agents; Scribe and Ralph remain fixed system roles.
- 2026-05-14T21:16:50.701+01:00 — PDFFlatten targets VB.NET on .NET Framework 4.6.2 and is focused on flattening PDF AcroForm fields for reliable iPhone printing.

- 2026-05-14T21:25:31.595+01:00 — PDFFlatten is scaffolded as a VB.NET class library targeting .NET Standard 2.0 so it builds on macOS via the dotnet CLI; PDF library selection is intentionally deferred until compatibility is reviewed.
- 2026-05-14T21:25:31.595+01:00 — User directive: build PDFFlatten as a portable .NET library instead of .NET Framework 4.6.2; Purah specializes in portability/runtime-compatibility decisions.
- 2026-05-14T21:25:31.595+01:00 — .NET Standard 2.0 chosen over .NET Framework (Windows-only, incompatible with macOS), .NET Standard 2.1 (breaks Framework consumers), or Modern .NET (loses portability); broadest reach across all .NET implementations while supporting macOS CLI builds.

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
