namespace CBAD.Models;

internal sealed class StationState
{
    public int Station { get; init; }
    public CadexRecord? Latest { get; set; }
    public Queue<CadexRecord> History { get; } = new();
    public Queue<string> RawLines { get; } = new();

    /// <summary>
    /// The wall-clock time when the current battery-service session started.
    /// Set when a session-start event code (11, 20, 200, 201) is received, or
    /// on the very first record for the station if no explicit start event was
    /// observed (e.g. when capture begins mid-session).
    /// </summary>
    public DateTimeOffset? SessionStart { get; set; }

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
    /// The most recent event code that is a known fault indicator (i.e. one
    /// whose code <see cref="CBAD.Parsing.CadexEventParser.DetermineFailureReason"/>
    /// resolves to a non-null description).  This is kept "sticky" so that
    /// intermediate non-fault status events — such as code 19 "Resting" emitted
    /// between a fault code and the final failure event — do not erase the true
    /// root-cause breadcrumb.
    /// </summary>
    public int? LastFaultEventCode { get; set; }

    /// <summary>
    /// The <see cref="CadexRecord.ProcessCode"/> that was active at the time
    /// <see cref="LastFaultEventCode"/> was recorded.  Used to detect when the
    /// station has transitioned to a new major testing phase so that faults
    /// from a previous phase can be cleared.
    /// </summary>
    public int? LastFaultProcessCode { get; set; }

    /// <summary>
    /// The <see cref="CadexRecord"/> corresponding to <see cref="LastFaultEventCode"/>.
    /// </summary>
    public CadexRecord? LastFaultRecord { get; set; }

    /// <summary>
    /// Human-readable description of the most recent failure, set when a
    /// <c>116</c> (Program Fail) or <c>16</c> (Custom Program Failed) event is
    /// received.  <c>null</c> when no failure has been detected or after a new
    /// session starts.
    /// </summary>
    public string? FailureReason { get; set; }

    /// <summary>
    /// <c>true</c> when the test reached its natural end-of-sequence event
    /// (e.g. event code 13 "Test Complete") without an early abort.
    /// Defaults to <c>true</c> so that diagnostic rules behave correctly when
    /// this flag has not been explicitly set (e.g. in unit tests that construct
    /// state directly).  Set to <c>false</c> when a session is known to have
    /// terminated abnormally — the Stage 4 analyzer will then skip
    /// capacity- and sag-based explanations that require a full test run to be
    /// meaningful.
    /// </summary>
    public bool TestCompletedNormally { get; set; } = true;
}
