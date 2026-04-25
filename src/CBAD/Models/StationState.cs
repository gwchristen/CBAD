namespace CBAD.Models;

internal sealed class StationState
{
    public int Station { get; init; }
    public CadexRecord? Latest { get; set; }
    public List<CadexRecord> History { get; } = new();
    public List<string> RawLines { get; } = new();
}
