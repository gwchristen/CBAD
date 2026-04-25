using System.Text;
using CBAD.Parsing;

namespace CBAD.Tests;

/// <summary>
/// Tests for SerialStreamProcessor — the async byte-stream-to-line pipeline seam.
/// A MemoryStream is used as the stream source so tests are deterministic and fast.
/// </summary>
public class SerialStreamProcessorTests
{
    // ---------------------------------------------------------------------------
    // Minimal ILineSink that captures written chunks in memory
    // ---------------------------------------------------------------------------

    private sealed class CaptureSink : ILineSink
    {
        public readonly List<string> Chunks = new();
        public string Path => string.Empty;
        public void Write(DateTimeOffset _, string dataChunk) => Chunks.Add(dataChunk);
        public void Dispose() { }
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static MemoryStream AsStream(string text, Encoding? enc = null)
        => new((enc ?? Encoding.ASCII).GetBytes(text));

    // ---------------------------------------------------------------------------
    // Tests
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SingleLine_EmitsOneRawLine()
    {
        var sink = new CaptureSink();
        var lines = new List<string>();

        using var stream = AsStream("hello\n");
        var processor = new SerialStreamProcessor(stream, Encoding.ASCII, sink,
            onRawLine: l => lines.Add(l));

        await processor.RunAsync(CancellationToken.None);

        Assert.Single(lines);
        Assert.Equal("hello", lines[0]);
    }

    [Fact]
    public async Task BurstInput_ManyLinesInOneRead_AllLinesEmitted()
    {
        // 10 complete lines in one contiguous block (fits in a single ReadAsync call
        // from MemoryStream).
        var sb = new System.Text.StringBuilder();
        for (int i = 1; i <= 10; i++)
            sb.Append($"line{i}\n");

        var sink = new CaptureSink();
        var lines = new List<string>();

        using var stream = AsStream(sb.ToString());
        var processor = new SerialStreamProcessor(stream, Encoding.ASCII, sink,
            onRawLine: l => lines.Add(l));

        await processor.RunAsync(CancellationToken.None);

        Assert.Equal(10, lines.Count);
        for (int i = 1; i <= 10; i++)
            Assert.Equal($"line{i}", lines[i - 1]);
    }

    [Fact]
    public async Task SplitLine_AcrossReads_ReassembledCorrectly()
    {
        // Force the line to arrive in two separate reads by using a custom stream.
        var sink = new CaptureSink();
        var lines = new List<string>();

        using var stream = new SplitStream("hel", "lo\n");
        var processor = new SerialStreamProcessor(stream, Encoding.ASCII, sink,
            onRawLine: l => lines.Add(l));

        await processor.RunAsync(CancellationToken.None);

        Assert.Single(lines);
        Assert.Equal("hello", lines[0]);
    }

    [Fact]
    public async Task CrLf_Endings_StrippedCorrectly()
    {
        var sink = new CaptureSink();
        var lines = new List<string>();

        using var stream = AsStream("alpha\r\nbeta\r\n");
        var processor = new SerialStreamProcessor(stream, Encoding.ASCII, sink,
            onRawLine: l => lines.Add(l));

        await processor.RunAsync(CancellationToken.None);

        Assert.Equal(2, lines.Count);
        Assert.Equal("alpha", lines[0]);
        Assert.Equal("beta", lines[1]);
    }

    [Fact]
    public async Task DataChunks_ForwardedToSink()
    {
        var sink = new CaptureSink();

        using var stream = AsStream("abc\ndef\n");
        var processor = new SerialStreamProcessor(stream, Encoding.ASCII, sink);

        await processor.RunAsync(CancellationToken.None);

        // MemoryStream returns all bytes in one read, so we expect one chunk.
        Assert.Single(sink.Chunks);
        Assert.Equal("abc\ndef\n", sink.Chunks[0]);
    }

    [Fact]
    public async Task OnData_Callback_InvokedForEachReadChunk()
    {
        var dataChunks = new List<string>();
        var sink = new CaptureSink();

        using var stream = new SplitStream("part1\n", "part2\n");
        var processor = new SerialStreamProcessor(stream, Encoding.ASCII, sink,
            onData: c => dataChunks.Add(c));

        await processor.RunAsync(CancellationToken.None);

        Assert.Equal(2, dataChunks.Count);
        Assert.Equal("part1\n", dataChunks[0]);
        Assert.Equal("part2\n", dataChunks[1]);
    }

    [Fact]
    public async Task Cancellation_StopsProcessingCleanly()
    {
        var sink = new CaptureSink();
        var lines = new List<string>();

        using var cts = new CancellationTokenSource();

        // Stream that blocks until cancellation — simulated by an infinite-wait stream.
        using var stream = new WaitForCancelStream(cts.Token);
        var processor = new SerialStreamProcessor(stream, Encoding.ASCII, sink,
            onRawLine: l => lines.Add(l));

        // Cancel after a short delay.
        cts.CancelAfter(50);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => processor.RunAsync(cts.Token));

        Assert.Empty(lines);
    }

    [Fact]
    public async Task EmptyStream_ReturnsWithoutEmittingLines()
    {
        var sink = new CaptureSink();
        var lines = new List<string>();

        using var stream = new MemoryStream();
        var processor = new SerialStreamProcessor(stream, Encoding.ASCII, sink,
            onRawLine: l => lines.Add(l));

        await processor.RunAsync(CancellationToken.None);

        Assert.Empty(lines);
        Assert.Empty(sink.Chunks);
    }

    // ---------------------------------------------------------------------------
    // Helper streams
    // ---------------------------------------------------------------------------

    /// <summary>A stream that returns its segments one at a time, then EOF.</summary>
    private sealed class SplitStream : Stream
    {
        private readonly Queue<byte[]> _segments;

        public SplitStream(params string[] parts)
        {
            _segments = new Queue<byte[]>(parts.Select(p => Encoding.ASCII.GetBytes(p)));
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_segments.Count == 0) return 0;
            var seg = _segments.Dequeue();
            int n = Math.Min(count, seg.Length);
            seg.AsSpan(0, n).CopyTo(buffer.AsSpan(offset, n));
            return n;
        }
    }

    /// <summary>A stream whose ReadAsync blocks until the given token is cancelled.</summary>
    private sealed class WaitForCancelStream : Stream
    {
        private readonly CancellationToken _token;
        public WaitForCancelStream(CancellationToken token) { _token = token; }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count)
        {
            _token.WaitHandle.WaitOne();
            _token.ThrowIfCancellationRequested();
            return 0;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
        {
            await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
            return 0;
        }
    }
}
