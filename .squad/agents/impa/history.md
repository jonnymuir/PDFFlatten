# Project Context

- **Owner:** Jonny Muir
- **Project:** PDFFlatten
- **Stack:** VB.NET, .NET Framework 4.6.2, PDF AcroForm/form-field flattening
- **Description:** A VB.NET utility that takes PDFs with form fields and flattens them so they print correctly from an iPhone.
- **Created:** 2026-05-14T21:16:50.701+01:00

## Learnings

- Team lead for scoping the implementation, reviewing architecture, and coordinating specialist handoffs.
- Key specialists on this project are Purah for VB/.NET Framework 4.6.2 and Zelda for PDF/AcroForm behavior.
- 2026-05-14T21:25:31.595+01:00 — Rebased the project direction onto a VB.NET `.NET Standard 2.0` class-library skeleton (`PDFFlatten.sln`, `src/PDFFlatten/PDFFlatten.vbproj`) because macOS rules out .NET Framework and the PDF engine choice should stay deferred behind `IPdfFlattener`.

**2026-05-14T20:25:31Z — Decision Merged & Orchestration Complete**
- Scribe merged Impa's project-structure decision into `decisions.md` alongside Purah's portable-target analysis.
- Orchestration log created: `.squad/orchestration-log/2026-05-14T20-25-31-Impa.md`.
- Both Purah and Impa decisions now in team record. Squad is ready for PDF library selection and implementation work.
