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
}
