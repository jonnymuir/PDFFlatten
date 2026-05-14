using NUnit.Framework;
using System.Reflection;
using System.Runtime.ExceptionServices;

using PDFFlatten;

namespace PDFFlatten.Tests;

public class FlattenApiContractTests
{
    [Test]
    public void Flatten_requires_a_non_null_input_stream()
    {
        var api = FlattenApiHarness.RequireImplemented();

        Assert.That(() => api.Flatten(null!), Throws.TypeOf<ArgumentNullException>());
    }

    [Test]
    public void Flatten_returns_a_readable_pdf_stream_without_closing_the_input()
    {
        var api = FlattenApiHarness.RequireImplemented();
        var input = new TrackingMemoryStream(TestAssets.LoadSamplePdfBytes());

        try
        {
            using var output = api.Flatten(input);

            Assert.Multiple(() =>
            {
                Assert.That(output, Is.Not.Null);
                Assert.That(output.CanRead, Is.True);
                Assert.That(output.CanSeek, Is.True, "Returned streams should be rewindable for callers.");
                Assert.That(output.Length, Is.GreaterThan(0));
                Assert.That(output.Position, Is.EqualTo(0));
                Assert.That(input.WasDisposed, Is.False, "Flatten should not dispose a caller-owned input stream.");
            });

            var flattenedBytes = ReadAllBytes(output);
            var probe = TestAssets.PdfProbe.FromBytes(flattenedBytes);

            Assert.That(probe.StartsWithPdfHeader, Is.True, "Flatten should still return a PDF.");
        }
        finally
        {
            input.Dispose();
        }
    }

    [Test]
    public void Flatten_output_overload_writes_to_the_caller_stream_without_disposing_either_side()
    {
        using var input = new TrackingMemoryStream(TestAssets.LoadSamplePdfBytes());
        using var output = new TrackingMemoryStream();

        PdfFlattener.Flatten(input, output);

        Assert.Multiple(() =>
        {
            Assert.That(input.WasDisposed, Is.False);
            Assert.That(output.WasDisposed, Is.False);
            Assert.That(output.Length, Is.GreaterThan(0));
            Assert.That(output.Position, Is.EqualTo(output.Length));
        });
    }

    private static byte[] ReadAllBytes(Stream stream)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private sealed class TrackingMemoryStream : MemoryStream
    {
        internal TrackingMemoryStream()
        {
        }

        internal TrackingMemoryStream(byte[] buffer) : base(buffer)
        {
        }

        internal bool WasDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            WasDisposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class FlattenApiHarness
    {
        private readonly MethodInfo _method;
        private readonly object? _target;

        private FlattenApiHarness(MethodInfo method, object? target)
        {
            _method = method;
            _target = target;
        }

        internal static FlattenApiHarness RequireImplemented()
        {
            var candidates = typeof(PdfFlattener)
                .Assembly
                .GetExportedTypes()
                .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
                .Where(method => method.Name.Equals("Flatten", StringComparison.Ordinal))
                .Where(method => typeof(Stream).IsAssignableFrom(method.ReturnType))
                .Where(method =>
                {
                    var parameters = method.GetParameters();
                    return parameters.Length == 1 && parameters[0].ParameterType == typeof(Stream);
                })
                .ToList();

            Assert.That(candidates, Has.Count.EqualTo(1), "Expected exactly one public Flatten(Stream) entry point.");

            var method = candidates.Single();
            object? target = null;

            if (!method.IsStatic)
            {
                var constructor = method.DeclaringType?.GetConstructor(Type.EmptyTypes);
                Assert.That(constructor, Is.Not.Null, "Instance-based Flatten(Stream) APIs must expose a public parameterless constructor for simple use.");
                target = constructor!.Invoke(null);
            }

            return new FlattenApiHarness(method, target);
        }

        internal Stream Flatten(Stream input)
        {
            try
            {
                return (Stream)(_method.Invoke(_target, new object?[] { input }) ?? throw new AssertionException("Flatten(Stream) returned null."));
            }
            catch (TargetInvocationException exception) when (exception.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
        }
    }
}
