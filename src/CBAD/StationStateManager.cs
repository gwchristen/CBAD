using CBAD.Models;
using CBAD.Parsing;

namespace CBAD;

/// <summary>
/// Manages live state for all four Cadex stations: ring-buffer history, latest
/// record, and raw-line capture.  Decoupled from UI so it can be unit-tested
/// independently.  All calls to <see cref="ProcessLine"/> are expected to
/// originate from the UI thread (via <c>BeginInvoke</c>) so no additional
/// synchronization is required.
/// </summary>
internal sealed class StationStateManager
{
    /// <summary>Maximum number of parsed records kept per station.</summary>
    public const int HistoryCapacity = 200;

    /// <summary>Maximum number of raw lines kept per station.</summary>
    public const int RawLineCapacity = 500;

    private readonly StationState[] _states =
    [
        new() { Station = 1 },
        new() { Station = 2 },
        new() { Station = 3 },
        new() { Station = 4 },
    ];

    /// <summary>Raised after a station's state has been updated.</summary>
    public event Action<StationState>? StationUpdated;

    /// <summary>Raised when a raw line could not be parsed.</summary>
    public event Action<string>? ParseFailed;

    /// <summary>Returns the state for the given 1-based station number.</summary>
    public StationState GetState(int station) => _states[station - 1];

    /// <summary>
    /// Parses <paramref name="line"/>, updates the matching station's ring
    /// buffers, and fires <see cref="StationUpdated"/>.  Lines that cannot be
    /// parsed fire <see cref="ParseFailed"/> instead.
    /// </summary>
    public void ProcessLine(string line)
    {
        var receivedAt = DateTimeOffset.UtcNow;
        var record = CadexRecordParser.TryParse(line, receivedAt);

        if (record is null)
        {
            ParseFailed?.Invoke(line);
            return;
        }

        var state = _states[record.Station - 1];

        state.Latest = record;

        // A new battery-service session resets any prior failure reason so the
        // UI does not persist stale error state from a previous test.
        if (CadexEventParser.IsSessionStartCode(record.EventCode))
        {
            state.FailureReason        = null;
            state.LastActiveEventCode  = null;
            state.LastActiveRecord     = null;
        }

        // Track the most recent non-telemetry event so that when a failure code
        // arrives the root cause can be identified from the last breadcrumb.
        if (record.EventCode != 250)
        {
            if (CadexEventParser.IsFailureCode(record.EventCode))
            {
                // Failure event: derive the reason from the preceding event code.
                var reason = state.LastActiveEventCode.HasValue
                    ? CadexEventParser.DetermineFailureReason(
                          state.LastActiveEventCode.Value, state.LastActiveRecord)
                    : null;

                state.FailureReason = reason ?? "Program Failed";
            }
            else
            {
                state.LastActiveEventCode = record.EventCode;
                state.LastActiveRecord    = record;
            }
        }

        // Persist the latest known target/measured capacity string so the UI
        // can display it even after the record that carried it has scrolled out
        // of the bounded history ring buffer.
        if (record.CapacityPayload is not null)
            state.TargetCapacity = record.CapacityPayload;
        else if (record.TargetCapacityPct.HasValue)
            state.TargetCapacity = $"{record.TargetCapacityPct}%";

        state.History.Add(record);
        if (state.History.Count > HistoryCapacity)
            state.History.RemoveAt(0);

        state.RawLines.Add(line);
        if (state.RawLines.Count > RawLineCapacity)
            state.RawLines.RemoveAt(0);

        StationUpdated?.Invoke(state);
    }
}
