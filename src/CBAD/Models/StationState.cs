namespace CBAD.Models;

internal sealed class StationState
{
    public int Station { get; init; }
    public CadexRecord? Latest { get; set; }
    public List<CadexRecord> History { get; } = new();
    public List<string> RawLines { get; } = new();

    /// <summary>
    /// Latest known target or measured capacity string, updated from each
    /// incoming record's <see cref="CadexRecord.CapacityPayload"/> (event 250)
    /// or <see cref="CadexRecord.TargetCapacityPct"/> (events 201 / 20).
    /// </summary>
    public string? TargetCapacity { get; set; }

    /// <summary>
    /// The most recent event code received that is not a normal-telemetry (250)
    /// record.  Tracked so that when a <c>116</c> (Program Fail) event arrives,
    /// the system can identify the true root cause.
    /// </summary>
    public int? LastActiveEventCode { get; set; }

    /// <summary>
    /// The <see cref="CadexRecord"/> corresponding to
    /// <see cref="LastActiveEventCode"/>, retained so payload values (e.g. the
    /// measured resistance for an OhmTest event) are available for failure
    /// reason formatting.
    /// </summary>
    public CadexRecord? LastActiveRecord { get; set; }

    /// <summary>
    /// Human-readable description of the most recent failure, set when a
    /// <c>116</c> (Program Fail) or <c>16</c> (Custom Program Failed) event is
    /// received.  <c>null</c> when no failure has been detected or after a new
    /// session starts.
    /// </summary>
    public string? FailureReason { get; set; }
}
