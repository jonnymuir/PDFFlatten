---
name: "netstandard-vb-library-skeleton"
description: "Scaffold or migrate a macOS-friendly portable .NET library without locking the repo to the wrong language/project shape"
domain: "architecture"
confidence: "high"
source: "earned"
---

## Context

Use this when a portability-first .NET library needs to run from the `dotnet` CLI on macOS or Linux and the concrete implementation dependencies are still undecided, or when the repo is migrating between VB.NET and C# without changing runtime reach.

## Patterns

- Target `.NET Standard 2.0` for the reusable library when broad runtime compatibility matters more than new platform-specific APIs.
- Create a solution root plus `src/{ProjectName}` library layout immediately so build commands and future test projects have a stable home.
- Keep the first public surface area to a narrow interface or contract, not a fake implementation backed by premature dependency choices.
- Defer PDF or other heavyweight package wiring until compatibility with the chosen target framework is verified.
- When porting between VB.NET and C#, switch solution entries, workflow pack paths, sample references, and README file paths in the same change.
- Update `.gitignore` for `bin/` and `obj/` as soon as the first SDK-style project is added.

## Examples

- `PDFFlatten.sln` at the repo root with `src/PDFFlatten/PDFFlatten.csproj`
- `IPdfFlattener` interface as the initial seam for a future PDF-engine adapter
- Root README that explains why `netstandard2.0` was chosen and how VB/.NET Framework consumers still call the C# library

## Anti-Patterns

- Starting with .NET Framework for a cross-platform project
- Picking a PDF library before confirming it supports the target framework
- Adding a test framework before there is meaningful behavior to verify
- Migrating source files to C# while leaving the solution, CI pack path, or sample project pointed at deleted `.vbproj` files
- Leaving build artifacts unignored after introducing SDK-style projects
