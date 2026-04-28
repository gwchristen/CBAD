using System.Collections.Concurrent;

namespace CBAD.Tests;

public class RawLineSinkTests : IDisposable
{
    private readonly string _tempDir;

    public RawLineSinkTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void WrittenData_AppearsWithTimestampPrefix()
    {
        var ts = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);
        string path;
        using (var sink = new RawLineSink(_tempDir, "test"))
        {
            sink.Write(ts, "some data");
            path = sink.Path;
        }

        var content = File.ReadAllText(path);
        Assert.StartsWith($"[{ts:O}] some data", content);
    }

    [Fact]
    public async Task ConcurrentWriteAndDispose_DoesNotThrow()
    {
        var sink = new RawLineSink(_tempDir, "concurrent");
        var ts = DateTimeOffset.UtcNow;
        var exceptions = new ConcurrentBag<Exception>();
        using var ready = new ManualResetEventSlim(false);

        var writeTask = Task.Run(() =>
        {
            ready.Wait();
            for (int i = 0; i < 200; i++)
            {
                try { sink.Write(ts, $"chunk {i}"); }
                catch (Exception ex) { exceptions.Add(ex); }
            }
        });

        var disposeTask = Task.Run(() =>
        {
            ready.Wait();
            try { sink.Dispose(); }
            catch (Exception ex) { exceptions.Add(ex); }
        });

        ready.Set();
        await Task.WhenAll(writeTask, disposeTask);
        Assert.Empty(exceptions);
    }

    [Fact]
    public void DoubleDispose_DoesNotThrow()
    {
        var sink = new RawLineSink(_tempDir, "double-dispose");

        sink.Dispose();
        var ex = Record.Exception(() => sink.Dispose());

        Assert.Null(ex);
    }

    [Fact]
    public void WriteAfterDispose_IsIgnoredSilently()
    {
        var sink = new RawLineSink(_tempDir, "write-after-dispose");
        sink.Dispose();

        var ex = Record.Exception(() => sink.Write(DateTimeOffset.UtcNow, "late data"));

        Assert.Null(ex);
    }
}
