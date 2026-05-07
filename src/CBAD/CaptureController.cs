using CBAD.Simulation;
using CBAD.UI;

namespace CBAD;

internal sealed class CaptureController
{
    private CancellationTokenSource? _cts;
    private Task? _captureTask;
    private ILineSink? _sink;
    private bool _simulationMode;
    private AppOptions _currentOptions = new();

    public event Action<CaptureLifecycleState, string?>? LifecycleChanged;
    public event Action<string>? RawLineReceived;
    public event Action<string>? StatusChanged;

    public bool IsRunning => _captureTask is not null;
    public bool IsSimulationMode => _simulationMode;
    public ILineSink? CurrentSink => _sink;

    public async Task StartAsync(AppOptions options, bool simulationMode)
    {
        if (_captureTask is not null)
            return;

        _currentOptions = options;
        _simulationMode = simulationMode;
        LifecycleChanged?.Invoke(CaptureLifecycleState.Starting, null);

        try
        {
            _sink = CreateSink();
            _cts = new CancellationTokenSource();

            if (_simulationMode)
            {
                AppLog.Info("Starting simulation mode");
                var sim = new SimulationService(
                    onRawLine: line => RawLineReceived?.Invoke(line),
                    onStatus: status => StatusChanged?.Invoke(status),
                    onData: null);

                _captureTask = Task.Run(() => sim.RunAsync(_cts.Token));
                LifecycleChanged?.Invoke(CaptureLifecycleState.Running, $"Logging to: {_sink.Path}");
            }
            else
            {
                AppLog.Info($"Starting capture on {_currentOptions.Port} @ {_currentOptions.Baud} baud");
                var sinkPath = _sink.Path;
                var service = new SerialCaptureService(
                    _currentOptions,
                    _sink,
                    onConnected: detail => LifecycleChanged?.Invoke(
                        CaptureLifecycleState.Running,
                        $"Connected: {detail} | Logging to: {sinkPath}"),
                    onData: null,
                    onStatus: status => StatusChanged?.Invoke(status),
                    onRawLine: line => RawLineReceived?.Invoke(line));

                _captureTask = Task.Run(() => service.RunAsync(_cts.Token));
            }

            await Task.Yield();
        }
        catch (Exception ex)
        {
            AppLog.Error("Cannot start capture", ex);
            LifecycleChanged?.Invoke(CaptureLifecycleState.Error, ex.Message);
            await StopCoreAsync(emitLifecycle: false);
            throw;
        }
    }

    public async Task StopAsync()
    {
        await StopCoreAsync(emitLifecycle: true);
    }

    private async Task StopCoreAsync(bool emitLifecycle)
    {
        var cts = _cts;
        var captureTask = _captureTask;
        var sink = _sink;

        _cts = null;
        _captureTask = null;
        _sink = null;

        if (cts is null && captureTask is null && sink is null)
            return;

        if (emitLifecycle)
            LifecycleChanged?.Invoke(CaptureLifecycleState.Stopping, null);

        try { cts?.Cancel(); }
        catch (Exception ex)
        {
            AppLog.Error("CancellationTokenSource.Cancel error", ex);
            System.Diagnostics.Debug.WriteLine($"Cancel error: {ex.Message}");
        }
        cts?.Dispose();

        bool hasError = false;
        if (captureTask is not null)
        {
            try { await captureTask; }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                hasError = true;
                AppLog.Error("Capture task stopped with error", ex);
                if (emitLifecycle)
                    LifecycleChanged?.Invoke(CaptureLifecycleState.Error, ex.Message);
            }
        }

        sink?.Dispose();

        AppLog.Info("Capture stopped");
        if (emitLifecycle && !hasError)
            LifecycleChanged?.Invoke(CaptureLifecycleState.Idle, "Stopped");
    }

    private ILineSink CreateSink()
    {
        var outDir = _currentOptions.OutDir?.Trim();
        if (string.IsNullOrWhiteSpace(outDir))
            throw new InvalidOperationException("Please select an output folder.");

        var prefix = _currentOptions.Prefix?.Trim();
        if (string.IsNullOrWhiteSpace(prefix))
            prefix = "cadex_raw";

        return _currentOptions.Csv
            ? new CsvLineSink(outDir, prefix)
            : new RawLineSink(outDir, prefix);
    }
}
