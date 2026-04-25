using System.Text;

namespace CBAD.Parsing;

/// <summary>
/// Reads bytes from a stream asynchronously, converts them to text using the supplied
/// encoding, reassembles newline-delimited lines, and forwards them through the provided
/// callbacks.  Extracted from SerialCaptureService as a testable seam.
/// </summary>
internal sealed class SerialStreamProcessor
{
    private readonly Stream _stream;
    private readonly Encoding _encoding;
    private readonly ILineSink _sink;
    private readonly Action<string>? _onData;
    private readonly Action<string>? _onRawLine;
    private readonly LineReassembler _reassembler = new();

    private const int BufferSize = 4096;

    public SerialStreamProcessor(
        Stream stream,
        Encoding encoding,
        ILineSink sink,
        Action<string>? onData = null,
        Action<string>? onRawLine = null)
    {
        _stream = stream;
        _encoding = encoding;
        _sink = sink;
        _onData = onData;
        _onRawLine = onRawLine;
    }

    /// <summary>
    /// Reads from the stream until EOF or cancellation, dispatching data through the
    /// sink and raw-line callbacks.  Returns when the stream reaches EOF, the
    /// cancellation token is signalled, or an exception propagates.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[BufferSize];

        while (!cancellationToken.IsCancellationRequested)
        {
            int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)
                .ConfigureAwait(false);

            if (bytesRead == 0)
                break; // EOF

            var chunk = _encoding.GetString(buffer, 0, bytesRead);
            var ts = DateTimeOffset.UtcNow;
            _sink.Write(ts, chunk);
            _onData?.Invoke(chunk);

            if (_onRawLine is not null)
            {
                foreach (var line in _reassembler.Feed(chunk))
                    _onRawLine(line);
            }
        }
    }
}
