---
name: "netstandard-vb-library-skeleton"
description: "Scaffold a macOS-friendly VB.NET library without committing to implementation dependencies too early"
domain: "architecture"
confidence: "high"
source: "earned"
---

## Context

Use this when a VB.NET project needs to run from the `dotnet` CLI on macOS or Linux and the concrete implementation dependencies are still undecided.

## Patterns

- Target `.NET Standard 2.0` for the initial reusable library when broad runtime compatibility matters more than new platform-specific APIs.
- Create a solution root plus `src/{ProjectName}` library layout immediately so build commands and future test projects have a stable home.
- Keep the first public surface area to a narrow interface or contract, not a fake implementation backed by premature dependency choices.
- Defer PDF or other heavyweight package wiring until compatibility with the chosen target framework is verified.
- Update `.gitignore` for `bin/` and `obj/` as soon as the first SDK-style project is added.

## Examples

- `PDFFlatten.sln` at the repo root with `src/PDFFlatten/PDFFlatten.vbproj`
- `IPdfFlattener` interface as the initial seam for a future PDF-engine adapter
- Root README that explains why `netstandard2.0` was chosen

## Anti-Patterns

- Starting with .NET Framework for a cross-platform project
- Picking a PDF library before confirming it supports the target framework
- Adding a test framework before there is meaningful behavior to verify
- Leaving build artifacts unignored after introducing SDK-style projects
