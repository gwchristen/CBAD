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
    private readonly LineReassembler _reassembler = new();

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

                while (!cancellationToken.IsCancellationRequested)
                {
                    var chunk = port.ReadExisting();
                    if (!string.IsNullOrEmpty(chunk))
                    {
                        var ts = DateTimeOffset.UtcNow;
                        _sink.Write(ts, chunk);
                        _onData?.Invoke(chunk);

                        if (_onRawLine is not null)
                        {
                            foreach (var line in _reassembler.Feed(chunk))
                                _onRawLine(line);
                        }
                    }
                    else
                    {
                        await Task.Delay(50, cancellationToken);
                    }
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
            ReadTimeout = 500,
            DtrEnable = false,
            RtsEnable = false
        };
    }
}
