namespace CBAD.Simulation;

internal sealed class SimulationService
{
    private const int TransitionCapacityValue = 70;

    private static readonly SimBattery[] Batteries =
    [
        new SimBattery(1, 16, 1962, 0, 26),
        new SimBattery(2, 16, 1769, 0, 24),
        new SimBattery(3, 16, 1865, 0, 27),
        new SimBattery(4, 16, 1862, 0, 37),
    ];

    private static readonly int[][] StatusSequences =
    [
        [45, 26,  2,  0],
        [ 2,  0,  1,  4],
        [ 4, 27,  5, 45],
        [ 0,  1, 37,  2],
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

        while (!ct.IsCancellationRequested)
        {
            var now = DateTime.Now;
            var dateStr = now.ToString("MM/dd/yyyy");
            var timeStr = now.ToString("HHmmss");

            for (int i = 0; i < Batteries.Length; i++)
            {
                var bat = Batteries[i];
                int statusCode = StatusSequences[i][tick % StatusSequences[i].Length];
                int cap = bat.BaseCapacity + _rng.Next(-5, 6);

                var line = $"0,{bat.Station},\"          \",\"{dateStr}\",\"{timeStr}\",250," +
                           $"\"{bat.TypeCode}\\{cap}\\{bat.Cycles}\\{bat.Health}\"," +
                           $"\"{statusCode}\"";

                _onRawLine?.Invoke(line);
                _onData?.Invoke(line);

                // 10% chance of a transition record — offset by station index for unique timestamps
                if (_rng.NextDouble() < 0.10)
                {
                    var transTime = now.AddSeconds(i + 1).ToString("HHmmss");
                    var transLine = $"0,{bat.Station},\"          \",\"{dateStr}\",\"{transTime}\",20," +
                                    $"\"{bat.TypeCode}\\{TransitionCapacityValue}\"";
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
