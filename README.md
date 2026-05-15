# PDFFlatten

PDFFlatten is a small **C# / .NET Standard 2.0** utility library for turning interactive **AcroForm PDFs** into plain, printable page content.

The library target stays at **`netstandard2.0`** so the same package can be consumed from **.NET Framework 4.6.1+** (including **4.6.2**) and current .NET releases.

## Why this package exists

Some PDFs print badly from mobile devices or lightweight viewers because the form fields remain interactive. PDFFlatten converts those widget appearances into ordinary page content so the rendered values travel with the page.

## Features

- `netstandard2.0` package for broad reuse
- simple `PdfFlattener.Flatten(Stream)` API
- overload for caller-owned output streams
- **in-house** flattening implementation with **no restrictive third-party PDF dependency**
- C# library source with one class per file and XML docs on the public surface
- generic-fixture and synthetic regression tests targeting `net10.0`
- NuGet package metadata, XML docs, symbols, SourceLink, and MIT licence
- GitHub Actions for CI, packaging, and tagged releases

## Install

```bash
dotnet add package PDFFlatten
```

## Compatibility posture

- **Library target:** `netstandard2.0`
- **Consumer reach:** designed for .NET Framework 4.6.2+ and current .NET runtimes through the .NET Standard 2.0 surface area
- **Repo validation target:** the sample app, tests, and CI currently run on `net10.0` as the repo's latest-SDK smoke-test lane; that does **not** change the package target or consumer matrix
- **Source layout:** C# implementation with one class per file, with public API kept intentionally small

## Quick start — modern .NET / C#

```csharp
using PDFFlatten;
using System.IO;

using Stream input = File.OpenRead("input.pdf");
using Stream flattened = PdfFlattener.Flatten(input);
using Stream output = File.Create("flattened.pdf");
flattened.CopyTo(output);
```

If you already manage the destination stream:

```csharp
using PDFFlatten;
using System.IO;

using Stream input = File.OpenRead("input.pdf");
using Stream output = File.Create("flattened.pdf");
PdfFlattener.Flatten(input, output);
```

## Quick start — VB.NET on .NET Framework 4.6.2

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

Caller-owned output stream example:

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

A minimal **C#** console app lives in `samples/PDFFlatten.Sample` for local CLI testing on macOS, Linux, or Windows.

```bash
dotnet run --project samples/PDFFlatten.Sample -- GenericAcroFormFixture.pdf GenericAcroFormFixture.flattened.pdf
```

The sample targets `net10.0` so local smoke tests run on the current SDK, while the reusable package remains `netstandard2.0` for consumer reach.

## Production-readiness posture

PDFFlatten is **production-ready for a constrained supported slice only**. If your PDFs stay inside the supported structures below and you validate them against your own corpus, this library is a reasonable production dependency. It is **not** positioned as a general-purpose "flatten arbitrary PDFs" engine.

## Supported structures (supported slice)

PDFFlatten currently supports:

- classic cross-reference-table PDFs (`xref` tables, not xref streams)
- single-revision files with no incremental-update trailer chain (`/Prev` absent)
- unencrypted AcroForm documents without `/XFA`
- streams whose `/Length` values are direct integers
- page dictionaries with direct `/Annots` arrays
- page dictionaries with their own `/Resources` dictionaries (no inherited page resources)
- widget annotations whose normal appearance at `/AP /N` resolves to a single indirect stream
- widgets/pages whose placement can be derived from `/Rect` and appearance `/BBox` without page rotation or appearance `/Matrix` transforms
- PDFs whose reachable indirect references resolve cleanly during serialization

This supported slice matches the current parser, flattener, and regression coverage. PDFs with no AcroForm/widgets are passed through unchanged.

## Rejection behavior

PDFFlatten is intentionally fail-closed outside the supported slice. It does **not** attempt a best-effort rewrite for known-unsupported structures.

- **`NotSupportedException`** is used for known out-of-scope structures such as xref streams, object streams, incremental-update trailers, encrypted files, XFA, inherited page resources, indirect page `/Annots`, non-stream `/AP /N`, appearance-state dictionaries, unsupported transforms, and unresolved indirect references during serialization.
- **`InvalidOperationException`** is used when the input is malformed or structurally incomplete for the supported parser (for example missing trailer data, malformed xref entries, or broken object boundaries).

For production callers, treat both exception types as input rejection signals and keep the original PDF untouched.

## Known limitations

- xref streams, hybrid-reference files, and object streams are unsupported
- incremental-update PDFs are unsupported; PDFFlatten expects a single classic trailer chain
- encrypted PDFs and XFA forms are unsupported
- streams with missing or indirect `/Length` values are unsupported
- indirect page `/Annots` arrays are unsupported
- pages that inherit `/Resources` are unsupported because flattening could shadow ancestor resources
- page rotation and appearance `/Matrix` transforms are unsupported
- stateful checkbox/radio appearance dictionaries are unsupported; `/AP /N` must resolve to a single indirect stream
- inherited field attributes from parent field dictionaries are **not** part of the current supported slice; that behavior remains open work
- real renderer/viewer equivalence is not yet automated; current validation is structural and fixture-based
- checked-in fixture diversity is still narrow; the repo does not yet prove broad producer coverage

## Not recommended for

- arbitrary user-supplied PDFs from unknown producer mixes
- signed or compliance-sensitive workflows where rewriting the PDF would invalidate signatures or require preserving revision history
- encrypted, XFA, transform-heavy, inherited-resource, or stateful-appearance forms
- deployments that cannot pre-validate inputs and quarantine rejected files
- teams that need a claim of broad Acrobat/browser/office producer coverage today

## Production deployment guidance

- test with your real PDF corpus before rollout; do not rely on the synthetic fixture alone
- keep a known-good list of producers/templates that fit the supported slice
- catch `NotSupportedException` and `InvalidOperationException`, log the message, and route rejected PDFs to a fallback/manual lane
- keep the source PDF so a rejected file can be retried after future library improvements
- verify flattened output in the viewers/printers you actually ship against; repo-side renderer/viewer equivalence automation is still pending

## Future enhancement areas

If you need confidence beyond the current supported slice, the next tracked work is:

- [issue #5](https://github.com/jonnymuir/PDFFlatten/issues/5) — curate a safe multi-producer AcroForm fixture corpus
- [issue #6](https://github.com/jonnymuir/PDFFlatten/issues/6) — add renderer/viewer-equivalence validation
- [issue #7](https://github.com/jonnymuir/PDFFlatten/issues/7) — resolve inherited field-attribute hierarchy behavior

## Project layout

```text
PDFFlatten.sln
src/
  PDFFlatten/
    PDFFlatten.csproj
    PdfFlattener.cs
    Internals/        # one class per file for internal model/parser/serializer types
samples/
  PDFFlatten.Sample/
    Program.cs
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
dotnet pack src/PDFFlatten/PDFFlatten.csproj --configuration Release --output artifacts
```

## Release flow

1. Update `CHANGELOG.md`.
2. Commit and push to GitHub.
3. Create a version tag like `v0.2.0`.
4. Push the tag.
5. GitHub Actions builds, tests, packs, creates a GitHub release, and publishes to NuGet.

```bash
git tag v0.2.0
git push origin v0.2.0
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
- If you publish under a different GitHub owner/repo, update the package metadata URLs in `src/PDFFlatten/PDFFlatten.csproj`.
