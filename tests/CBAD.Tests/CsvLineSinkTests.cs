using System.Collections.Concurrent;

namespace CBAD.Tests;

public class CsvLineSinkTests : IDisposable
{
    private readonly string _tempDir;

    public CsvLineSinkTests()
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
    public void HeaderRow_IsWrittenOnConstruction()
    {
        string path;
        using (var sink = new CsvLineSink(_tempDir, "test"))
            path = sink.Path;

        var lines = File.ReadAllLines(path);
        Assert.Single(lines);
        Assert.Equal("timestamp_utc,data", lines[0]);
    }

    [Fact]
    public void WrittenLine_AppearsInCsvWithCorrectFormat()
    {
        var ts = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);
        string path;
        using (var sink = new CsvLineSink(_tempDir, "test"))
        {
            sink.Write(ts, "hello");
            path = sink.Path;
        }

        var lines = File.ReadAllLines(path);
        Assert.Equal(2, lines.Length);
        Assert.Equal($"{ts:O},\"hello\"", lines[1]);
    }

    [Fact]
    public void NewlineNormalization_CrLf_SplitsIntoSeparateRows()
    {
        var ts = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);
        string path;
        using (var sink = new CsvLineSink(_tempDir, "test"))
        {
            sink.Write(ts, "line1\r\nline2");
            path = sink.Path;
        }

        var lines = File.ReadAllLines(path);
        Assert.Equal(3, lines.Length); // header + 2 data rows
        Assert.Contains("\"line1\"", lines[1]);
        Assert.Contains("\"line2\"", lines[2]);
    }

    [Fact]
    public void EmptyChunk_IsSkipped()
    {
        var ts = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);
        string path;
        using (var sink = new CsvLineSink(_tempDir, "test"))
        {
            sink.Write(ts, "");
            path = sink.Path;
        }

        var lines = File.ReadAllLines(path);
        Assert.Single(lines); // only the header
    }

    [Fact]
    public void QuotesInData_AreEscaped()
    {
        var ts = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);
        string path;
        using (var sink = new CsvLineSink(_tempDir, "test"))
        {
            sink.Write(ts, "say \"hello\"");
            path = sink.Path;
        }

        var lines = File.ReadAllLines(path);
        Assert.Equal(2, lines.Length);
        Assert.Contains("\"say \"\"hello\"\"\"", lines[1]);
    }

    [Fact]
    public async Task ConcurrentWriteAndDispose_DoesNotThrow()
    {
        var sink = new CsvLineSink(_tempDir, "concurrent");
        var ts = DateTimeOffset.UtcNow;
        var exceptions = new ConcurrentBag<Exception>();
        using var ready = new ManualResetEventSlim(false);

        var writeTask = Task.Run(() =>
        {
            ready.Wait();
            for (int i = 0; i < 200; i++)
            {
                try { sink.Write(ts, $"line {i}"); }
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
        var sink = new CsvLineSink(_tempDir, "double-dispose");

        sink.Dispose();
        var ex = Record.Exception(() => sink.Dispose());

        Assert.Null(ex);
    }

    [Fact]
    public void WriteAfterDispose_IsIgnoredSilently()
    {
        var sink = new CsvLineSink(_tempDir, "write-after-dispose");
        sink.Dispose();

        var ex = Record.Exception(() => sink.Write(DateTimeOffset.UtcNow, "late data"));

        Assert.Null(ex);
    }
}
