# PDFFlatten

PDFFlatten is a small **VB.NET / .NET Standard 2.0** utility library for turning interactive **AcroForm PDFs** into plain, printable page content.

The first public API is intentionally simple: give it a `Stream`, get back a flattened PDF `Stream`.

## Why this package exists

Some PDFs print badly from mobile devices or lightweight viewers because the form fields remain interactive. PDFFlatten converts those widget appearances into ordinary page content so the rendered values travel with the page.

## Features

- `netstandard2.0` package for broad reuse
- simple `PdfFlattener.Flatten(Stream)` API
- overload for caller-owned output streams
- **in-house** flattening implementation with **no restrictive third-party PDF dependency**
- generic-fixture and synthetic regression tests
- NuGet package metadata, XML docs, symbols, SourceLink, and MIT licence
- GitHub Actions for CI, packaging, and tagged releases

## Install

```bash
dotnet add package PDFFlatten
```

## Quick start

```vb
Imports PDFFlatten
Imports System.IO

Using input As Stream = File.OpenRead("input.pdf")
    Using flattened As Stream = PdfFlattener.Flatten(input)
        Using output As Stream = File.Create("flattened.pdf")
            flattened.CopyTo(output)
        End Using
    End Using
End Using
```

If you already manage the destination stream:

```vb
Imports PDFFlatten
Imports System.IO

Using input As Stream = File.OpenRead("input.pdf")
    Using output As Stream = File.Create("flattened.pdf")
        PdfFlattener.Flatten(input, output)
    End Using
End Using
```

## Console sample

A minimal VB.NET console app lives in `samples/PDFFlatten.Sample` for local CLI testing on macOS, Linux, or Windows.

```bash
dotnet run --project samples/PDFFlatten.Sample -- GenericAcroFormFixture.pdf GenericAcroFormFixture.flattened.pdf
```

## Supported scope in v0.1

PDFFlatten currently targets a practical first slice:

- classic cross-reference-table PDFs
- AcroForm widget annotations with normal appearance streams (`/AP /N`)
- flattening by replaying those appearance streams onto the page content

That covers the included synthetic AcroForm fixture and keeps the public API stable while the engine grows.

## Project layout

```text
PDFFlatten.sln
src/
  PDFFlatten/
    PDFFlatten.vbproj
    PdfFlattener.vb
    Internals/
samples/
  PDFFlatten.Sample/
    Program.vb
tests/
  PDFFlatten.Tests/
docs/
  github-setup.md
CHANGELOG.md
CONTRIBUTING.md
```

## Build, test, and pack locally

```bash
dotnet restore
dotnet build PDFFlatten.sln --configuration Release
dotnet test PDFFlatten.sln --configuration Release
dotnet pack src/PDFFlatten/PDFFlatten.vbproj --configuration Release --output artifacts
```

## Release flow

1. Update `CHANGELOG.md`.
2. Commit and push to GitHub.
3. Create a version tag like `v0.1.0`.
4. Push the tag.
5. GitHub Actions builds, tests, packs, creates a GitHub release, and publishes to NuGet.

```bash
git tag v0.1.0
git push origin v0.1.0
```

## GitHub setup

Use the full walkthrough in [`docs/github-setup.md`](docs/github-setup.md).

In short, you need to:

1. create the GitHub repo
2. push `main`
3. add the `NUGET_API_KEY` repository secret
4. enable Actions
5. tag releases with `v*`

## CI/CD included

- **CI**: restore, build, test, pack validation
- **Release**: build, test, pack, GitHub release, NuGet publish

## Contributing and maintenance

- contribution guide: [`CONTRIBUTING.md`](CONTRIBUTING.md)
- release notes scaffold: [`CHANGELOG.md`](CHANGELOG.md)
- GitHub bootstrap guide: [`docs/github-setup.md`](docs/github-setup.md)

## Notes

- Caller-owned streams are left open.
- The synthetic PDF fixture is covered by automated tests.
- If you publish under a different GitHub owner/repo, update the package metadata URLs in `src/PDFFlatten/PDFFlatten.vbproj`.
