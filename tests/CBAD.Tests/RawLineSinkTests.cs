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
        using var sink = new RawLineSink(_tempDir, "test");
        var ts = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

        sink.Write(ts, "some data");

        var content = File.ReadAllText(sink.Path);
        Assert.StartsWith($"[{ts:O}] some data", content);
    }
}
