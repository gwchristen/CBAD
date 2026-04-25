using CBAD;

namespace CBAD.Simulation;

internal sealed class SimulationService
{
    // Each SimBattery: Station, ProcessCode, BaseVoltage, Current, Temp, Health
    private static readonly SimBattery[] Batteries =
    [
        new SimBattery(1,  2, 3943,   796, 26, "89\\87"),
        new SimBattery(2,  7, 1419,  -401, 35, "85\\37"),
        new SimBattery(3,  4, 3500,   200, 28, "76\\67"),
        new SimBattery(4,  0, 3800,     0, 24, "40\\45"),
    ];

    // Each station cycles through a list of (processCode, health) tuples
    private static readonly (int process, string health)[][] ProcessSequences =
    [
        [(2, "89\\87"), (2, "90\\87"), (5, "90\\87"), (0, "90\\87")],
        [(7, "85\\37"), (7, "83\\37"), (0, "83\\37"), (1, "83\\37")],
        [(4, "76\\67"), (3, "77\\67"), (5, "78\\67"), (0, "78\\67")],
        [(0, "40\\45"), (1, "40\\45"), (2, "41\\45"), (7, "41\\45")],
    ];

    private readonly Action<string>? _onRawLine;
    private readonly Action<string>? _onStatus;
    private readonly Action<string>? _onData;
    private readonly int _tickMs;
    private readonly Random _rng = new();

    public SimulationService(
        Action<string>? onRawLine,
        Action<string>? onStatus,
        Action<string>? onData,
        int tickMs = 3000)
    {
        _onRawLine = onRawLine;
        _onStatus  = onStatus;
        _onData    = onData;
        _tickMs    = tickMs;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        int tick = 0;
        AppLog.Info("Simulation started");

        while (!ct.IsCancellationRequested)
        {
            var now = DateTime.Now;
            var dateStr = now.ToString("MM/dd/yyyy");
            var timeStr = now.ToString("HHmmss");

            for (int i = 0; i < Batteries.Length; i++)
            {
                var bat = Batteries[i];
                var (process, health) = ProcessSequences[i][tick % ProcessSequences[i].Length];
                int voltage = bat.BaseVoltage + _rng.Next(-5, 6);

                var line = $"0,{bat.Station},\"          \",\"{dateStr}\",\"{timeStr}\",250," +
                           $"\"{process}\\{voltage}\\{bat.Current}\\{bat.Temp}\"," +
                           $"\"{health}\"";

                _onRawLine?.Invoke(line);
                _onData?.Invoke(line);

                // 10% chance of a pre-test transition record (adapter inserted)
                if (_rng.NextDouble() < 0.10)
                {
                    var transTime = now.AddSeconds(i + 1).ToString("HHmmss");
                    var transLine = $"0,{bat.Station},\"          \",\"{dateStr}\",\"{transTime}\",201," +
                                    $"\"{process}\\80\"";
                    _onRawLine?.Invoke(transLine);
                    _onData?.Invoke(transLine);
                }
            }

            if (tick % 5 == 0)
            {
                _onStatus?.Invoke($"[SIM] Tick {tick} - {now:HH:mm:ss}");
            }

            tick++;

            await Task.Delay(_tickMs, ct);
        }
    }
}
