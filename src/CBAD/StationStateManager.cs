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
    public const int HistoryCapacity = 5000;

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
            state.LastFaultEventCode   = null;
            state.LastFaultRecord      = null;
            state.LastFaultProcessCode = null;
            state.SessionStart         = receivedAt;
        }
        else if (state.SessionStart is null)
        {
            // Capture started mid-session: record the first-seen timestamp as a
            // best-effort session start so that runtime can still be displayed.
            state.SessionStart = receivedAt;
        }

        // When the station transitions into a new major testing phase (Charge,
        // Discharge, Reconditioning), any sticky fault from a previous phase is
        // no longer the root cause for the new phase.  Clear it so that the
        // failure-reason logic can correctly attribute subsequent failures to
        // events that occur in the current phase.
        if (!CadexEventParser.IsFailureCode(record.EventCode)
            && !CadexEventParser.IsSessionStartCode(record.EventCode)
            && record.ProcessCode.HasValue
            && CadexEventParser.IsMajorPhase(record.ProcessCode.Value)
            && state.LastFaultEventCode.HasValue
            && record.ProcessCode.Value != state.LastFaultProcessCode)
        {
            state.LastFaultEventCode   = null;
            state.LastFaultRecord      = null;
            state.LastFaultProcessCode = null;
        }

        // Track the most recent non-telemetry event so that when a failure code
        // arrives the root cause can be identified from the last breadcrumb.
        if (record.EventCode != 250)
        {
            if (CadexEventParser.IsFailureCode(record.EventCode))
            {
                // Failure event: prefer the sticky fault indicator (if any) over the
                // generic last-active event, because intermediate status codes such as
                // code 19 "Resting" can arrive between a fault code and the failure
                // event and would otherwise erase the true root cause.
                var faultCode   = state.LastFaultEventCode ?? state.LastActiveEventCode;
                var faultRecord = state.LastFaultEventCode.HasValue
                    ? state.LastFaultRecord
                    : state.LastActiveRecord;

                var reason = faultCode.HasValue
                    ? CadexEventParser.DetermineFailureReason(faultCode.Value, faultRecord)
                    : null;

                state.FailureReason = reason ?? "Program Failed";
            }
            else
            {
                state.LastActiveEventCode = record.EventCode;
                state.LastActiveRecord    = record;

                // If this event code is a known terminal-fault indicator, keep it sticky so
                // that subsequent non-fault status events do not overwrite it before
                // the final failure event (16 / 116) arrives.
                if (CadexEventParser.IsFaultIndicatorCode(record.EventCode))
                {
                    state.LastFaultEventCode   = record.EventCode;
                    state.LastFaultRecord      = record;
                    state.LastFaultProcessCode = record.ProcessCode;
                }
            }
        }

        // Persist the latest known target/measured capacity string so the UI
        // can display it even after the record that carried it has scrolled out
        // of the bounded history ring buffer.
        if (record.CapacityPayload is not null)
            state.TargetCapacity = record.CapacityPayload;
        else if (record.TargetCapacityPct.HasValue)
            state.TargetCapacity = $"{record.TargetCapacityPct}%";

        state.History.Enqueue(record);
        if (state.History.Count > HistoryCapacity)
            state.History.Dequeue();

        state.RawLines.Enqueue(line);
        if (state.RawLines.Count > RawLineCapacity)
            state.RawLines.Dequeue();

        StationUpdated?.Invoke(state);
    }
}
