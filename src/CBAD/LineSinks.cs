namespace CBAD;

internal interface ILineSink : IDisposable
{
    string Path { get; }
    void Write(DateTimeOffset timestamp, string dataChunk);
}

internal sealed class RawLineSink : ILineSink
{
    private readonly StreamWriter _writer;
    public string Path { get; }

    public RawLineSink(string outDir, string prefix)
    {
        Directory.CreateDirectory(outDir);
        Path = System.IO.Path.Combine(outDir, $"{prefix}_{DateTime.Now:yyyyMMdd_HHmmss}.log");
        _writer = new StreamWriter(Path, append: true);
        _writer.AutoFlush = true;
    }

    public void Write(DateTimeOffset timestamp, string dataChunk)
    {
        _writer.Write($"[{timestamp:O}] ");
        _writer.Write(dataChunk);
    }

    public void Dispose() => _writer.Dispose();
}

internal sealed class CsvLineSink : ILineSink
{
    private readonly StreamWriter _writer;
    public string Path { get; }

    public CsvLineSink(string outDir, string prefix)
    {
        Directory.CreateDirectory(outDir);
        Path = System.IO.Path.Combine(outDir, $"{prefix}_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        _writer = new StreamWriter(Path, append: true);
        _writer.AutoFlush = true;
        _writer.WriteLine("timestamp_utc,data");
    }

    public void Write(DateTimeOffset timestamp, string dataChunk)
    {
        var normalized = dataChunk.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = normalized.Split('\n');
        foreach (var line in lines)
        {
            if (line.Length == 0) continue;
            var escaped = line.Replace("\"", "\"\"");
            _writer.WriteLine($"{timestamp:O},\"{escaped}\"");
        }
    }

    public void Dispose() => _writer.Dispose();
}
