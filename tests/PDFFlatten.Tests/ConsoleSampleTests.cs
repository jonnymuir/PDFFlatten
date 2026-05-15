using System.Diagnostics;
using NUnit.Framework;

namespace PDFFlatten.Tests;

public sealed class ConsoleSampleTests
{
    [Test]
    public void Sample_console_requires_input_and_output_arguments()
    {
        var result = RunSample();

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.StandardError, Does.Contain("PDFFlatten sample console"));
            Assert.That(result.StandardError, Does.Contain("Usage:"));
        });
    }

    [Test]
    public void Sample_console_flattens_the_generic_fixture_from_command_line()
    {
        var outputDirectory = Path.Combine(
            TestAssets.RepositoryRoot,
            "artifacts",
            "test-output",
            "sample-cli-output",
            TestContext.CurrentContext.Test.ID);
        var outputPath = Path.Combine(outputDirectory, "GenericAcroFormFixture.flattened.pdf");

        Directory.CreateDirectory(outputDirectory);

        try
        {
            var result = RunSample(TestAssets.SamplePdfPath, outputPath);

            Assert.Multiple(() =>
            {
                Assert.That(result.ExitCode, Is.EqualTo(0), result.StandardError);
                Assert.That(result.StandardOutput, Does.Contain("Flattened"));
                Assert.That(File.Exists(outputPath), Is.True, "Expected the sample app to create the requested output PDF.");
            });

            var probe = TestAssets.PdfProbe.FromBytes(File.ReadAllBytes(outputPath));

            Assert.Multiple(() =>
            {
                Assert.That(probe.StartsWithPdfHeader, Is.True);
                Assert.That(probe.HasAcroForm, Is.False);
                Assert.That(probe.WidgetCount, Is.EqualTo(0));
                Assert.That(probe.DrawOperationCount, Is.GreaterThan(0));
            });
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    private static SampleRunResult RunSample(params string[] args)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = TestAssets.RepositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(TestAssets.SampleProjectPath);
        startInfo.ArgumentList.Add("--configuration");
        startInfo.ArgumentList.Add("Release");
        startInfo.ArgumentList.Add("--");

        foreach (var argument in args)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new AssertionException("Failed to start the sample console.");

        if (!process.WaitForExit(120000))
        {
            process.Kill(entireProcessTree: true);
            throw new AssertionException("Timed out waiting for the sample console to finish.");
        }

        return new SampleRunResult(
            process.ExitCode,
            process.StandardOutput.ReadToEnd(),
            process.StandardError.ReadToEnd());
    }

    private sealed record SampleRunResult(int ExitCode, string StandardOutput, string StandardError);
}
