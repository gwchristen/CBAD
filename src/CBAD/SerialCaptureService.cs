using System.IO.Ports;
using System.Text;
using CBAD.Parsing;

namespace CBAD;

internal sealed class SerialCaptureService
{
    private readonly AppOptions _options;
    private readonly ILineSink _sink;
    private readonly Action<string>? _onData;
    private readonly Action<string>? _onStatus;
    private readonly Action<string>? _onRawLine;

    public SerialCaptureService(
        AppOptions options,
        ILineSink sink,
        Action<string>? onData = null,
        Action<string>? onStatus = null,
        Action<string>? onRawLine = null)
    {
        _options = options;
        _sink = sink;
        _onData = onData;
        _onStatus = onStatus;
        _onRawLine = onRawLine;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            using var port = BuildPort(_options);

            try
            {
                port.Open();
                _onStatus?.Invoke($"Connected: {_options.Port} @ {_options.Baud} baud");

                // Register a callback so that cancellation closes the port, which unblocks
                // any pending ReadAsync on the base stream promptly.
                using var reg = cancellationToken.Register(() =>
                {
                    if (port.IsOpen)
                        try { port.Close(); } catch { }
                });

                var processor = new SerialStreamProcessor(
                    port.BaseStream,
                    port.Encoding,
                    _sink,
                    onData: _onData,
                    onRawLine: _onRawLine);

                try
                {
                    await processor.RunAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception) when (cancellationToken.IsCancellationRequested)
                {
                    // Port was closed by the cancellation callback — treat as clean stop.
                    throw new OperationCanceledException(cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _onStatus?.Invoke($"Serial error: {ex.Message}");
                if (!_options.Reconnect)
                    throw;
            }
            finally
            {
                if (port.IsOpen)
                {
                    try { port.Close(); } catch { }
                }
            }

            if (!_options.Reconnect || cancellationToken.IsCancellationRequested)
                break;

            _onStatus?.Invoke($"Reconnecting in {_options.ReconnectDelayMs}ms...");
            await Task.Delay(_options.ReconnectDelayMs, cancellationToken);
        }
    }

    private static SerialPort BuildPort(AppOptions options)
    {
        return new SerialPort(options.Port!, options.Baud, options.Parity, options.DataBits, options.StopBits)
        {
            Handshake = options.Handshake,
            Encoding = Encoding.ASCII,
            ReadTimeout = SerialPort.InfiniteTimeout,
            DtrEnable = false,
            RtsEnable = false
        };
    }
}
