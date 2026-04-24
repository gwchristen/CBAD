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
        using var sink = new CsvLineSink(_tempDir, "test");

        var lines = File.ReadAllLines(sink.Path);
        Assert.Single(lines);
        Assert.Equal("timestamp_utc,data", lines[0]);
    }

    [Fact]
    public void WrittenLine_AppearsInCsvWithCorrectFormat()
    {
        using var sink = new CsvLineSink(_tempDir, "test");
        var ts = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

        sink.Write(ts, "hello");

        var lines = File.ReadAllLines(sink.Path);
        Assert.Equal(2, lines.Length);
        Assert.Equal($"{ts:O},\"hello\"", lines[1]);
    }

    [Fact]
    public void NewlineNormalization_CrLf_SplitsIntoSeparateRows()
    {
        using var sink = new CsvLineSink(_tempDir, "test");
        var ts = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

        sink.Write(ts, "line1\r\nline2");

        var lines = File.ReadAllLines(sink.Path);
        Assert.Equal(3, lines.Length); // header + 2 data rows
        Assert.Contains("\"line1\"", lines[1]);
        Assert.Contains("\"line2\"", lines[2]);
    }

    [Fact]
    public void EmptyChunk_IsSkipped()
    {
        using var sink = new CsvLineSink(_tempDir, "test");
        var ts = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

        sink.Write(ts, "");

        var lines = File.ReadAllLines(sink.Path);
        Assert.Single(lines); // only the header
    }

    [Fact]
    public void QuotesInData_AreEscaped()
    {
        using var sink = new CsvLineSink(_tempDir, "test");
        var ts = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

        sink.Write(ts, "say \"hello\"");

        var lines = File.ReadAllLines(sink.Path);
        Assert.Equal(2, lines.Length);
        Assert.Contains("\"say \"\"hello\"\"\"", lines[1]);
    }
}
