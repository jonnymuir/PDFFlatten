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
- generic-fixture, producer-corpus, and synthetic regression tests targeting `net10.0`
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

Fallback example for rejected PDFs:

```vb
Imports PDFFlatten
Imports System
Imports System.IO

Dim inputPath = "input.pdf"
Dim outputPath = "flattened-or-original.pdf"

Try
    Using input As Stream = File.OpenRead(inputPath)
        Using output As Stream = File.Create(outputPath)
            PdfFlattener.Flatten(input, output)
        End Using
    End Using
Catch ex As NotSupportedException
    Console.Error.WriteLine($"Warning: PDF left unflattened because it is outside PDFFlatten's supported slice. {ex.Message}")
    File.Copy(inputPath, outputPath, overwrite:=True)
Catch ex As InvalidOperationException
    Console.Error.WriteLine($"Warning: PDF left unflattened because PDFFlatten rejected the file as malformed or incomplete. {ex.Message}")
    File.Copy(inputPath, outputPath, overwrite:=True)
End Try
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
- source PDFs, direct stream payloads, and Flate-decoded appearance inspection payloads that stay within the library's explicit in-memory safety caps
- page dictionaries with direct `/Annots` arrays
- page dictionaries with their own `/Resources` dictionaries (no inherited page resources)
- field hierarchies whose parent dictionaries contribute naming only; operative widget/terminal-field attributes stay self-contained
- widget annotations whose normal appearance at `/AP /N` resolves to a single indirect stream
- widgets/pages whose placement can be derived from `/Rect` and appearance `/BBox` without page rotation or appearance `/Matrix` transforms
- PDFs whose reachable indirect references resolve cleanly during serialization

This supported slice matches the current parser, flattener, and regression coverage. PDFs with no AcroForm/widgets are passed through unchanged.

## Rejection behavior

PDFFlatten is intentionally fail-closed outside the supported slice. It does **not** attempt a best-effort rewrite for known-unsupported structures.

- **`NotSupportedException`** is used for known out-of-scope structures such as xref streams, object streams, incremental-update trailers, encrypted files, XFA, inherited page resources, inherited operative field attributes (`/FT`, `/DA`, `/DR`, `/V`), indirect page `/Annots`, non-stream `/AP /N`, appearance-state dictionaries, unsupported transforms, unresolved indirect references during serialization, and inputs or stream payloads that exceed the library's explicit in-memory safety caps.
- **`InvalidOperationException`** is used when the input is malformed or structurally incomplete for the supported parser (for example missing trailer data, malformed xref entries, or broken object boundaries).

For production callers, treat both exception types as input rejection signals and keep the original PDF untouched.

## Known limitations

- xref streams, hybrid-reference files, and object streams are unsupported
- incremental-update PDFs are unsupported; PDFFlatten expects a single classic trailer chain
- encrypted PDFs and XFA forms are unsupported
- streams with missing or indirect `/Length` values are unsupported
- very large source PDFs, direct stream payloads, or Flate-decoded appearance payloads are rejected once they exceed the library's explicit in-memory safety caps
- indirect page `/Annots` arrays are unsupported
- pages that inherit `/Resources` are unsupported because flattening could shadow ancestor resources
- page rotation and appearance `/Matrix` transforms are unsupported
- stateful checkbox/radio appearance dictionaries are unsupported; `/AP /N` must resolve to a single indirect stream
- operative field attributes inherited from parent field dictionaries are rejected fail-closed; only name-only parent hierarchies are in scope today
- automated visual-equivalence checks use macOS Quick Look (`qlmanage`) to rasterize covered supported-slice fixtures and compare original vs. flattened first-page output
- those visual checks are skipped on runners where Quick Look is unavailable; a dedicated macOS CI lane executes them
- checked-in producer coverage is still intentionally small: Quartz plus a sanitized ReportLab/pdfrw/pypdf corpus. That is useful regression coverage, not a claim of broad Acrobat/Office compatibility.

## Not recommended for

- arbitrary user-supplied PDFs from unknown producer mixes
- signed or compliance-sensitive workflows where rewriting the PDF would invalidate signatures or require preserving revision history
- encrypted, XFA, transform-heavy, inherited-resource, inherited-operative-field-attribute, or stateful-appearance forms
- deployments that cannot pre-validate inputs and quarantine rejected files
- teams that need a claim of broad Acrobat/browser/office producer coverage today

## Production deployment guidance

- test with your real PDF corpus before rollout; do not rely on the synthetic fixture alone
- keep a known-good list of producers/templates that fit the supported slice
- catch `NotSupportedException` and `InvalidOperationException`, log the message, and route rejected PDFs to a fallback/manual lane
- keep the source PDF so a rejected file can be retried after future library improvements
- verify flattened output in the viewers/printers you actually ship against; repo automation currently proves only that macOS Quick Look shows zero first-page pixel delta for the covered supported-slice fixtures

## Fixture corpus

The repo now includes a checked-in, non-sensitive producer corpus under `tests/PDFFlatten.Tests/Fixtures/ProducerCorpus` with a matching `provenance.json` manifest.

- `reportlab-textfields-raw.pdf` and `pdfrw-textfields-raw.pdf` intentionally stay just outside the current parser slice so tests lock in fail-closed rejection on leading-dot numeric tokens.
- `reportlab-textfields-classic-xref.pdf`, `pdfrw-textfields-classic-xref.pdf`, and `pypdf-textfields-classic-xref.pdf` are sanitized classic-xref fixtures that stay inside the supported slice and must flatten successfully.
- Every corpus fixture is locally generated and limited to two generic placeholder fields: `Name = Alice`, `City = Hyrule`.

## Renderer/viewer-equivalence checks

The test suite includes a visual-equivalence harness for the supported slice.

- **Renderer choice:** macOS Quick Look (`qlmanage`) because it is already present on macOS runners and adds no production dependency to the library
- **Comparable artifact:** a 2048px PNG thumbnail of the first page for both the original PDF and the flattened output
- **Tolerance policy:** the generic checked-in fixture requires **zero differing pixels**; the synthetic transform-sensitive case allows up to **1% differing pixels** because Quick Look is stable but not bit-exact when it rasterizes transformed live widgets versus replayed page XObjects
- **Coverage today:** the checked-in generic fixture plus a synthetic transform-sensitive text-field case
- **Skip policy:** the visual tests are skipped when Quick Look is unavailable (for example the Linux CI lane); the repo's macOS CI job is the lane that must keep them green

This is a repository confidence check, not a claim of universal viewer parity. It does not yet prove equivalence across Acrobat, browser viewers, Windows print stacks, or unsupported PDF structures.

## Future enhancement areas

If you need confidence beyond the current supported slice, the next tracked work is:

- [issue #6](https://github.com/jonnymuir/PDFFlatten/issues/6) — add renderer/viewer-equivalence validation

Potential future enhancement beyond the current boundary:

- full support for operative field-attribute inheritance across parent field dictionaries, but only after renderer-level corpus validation proves it preserves visual correctness

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
- The generic sample fixture and producer corpus are covered by automated tests.
- If you publish under a different GitHub owner/repo, update the package metadata URLs in `src/PDFFlatten/PDFFlatten.csproj`.
