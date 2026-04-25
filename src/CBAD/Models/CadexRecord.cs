namespace CBAD.Models;

internal sealed class CadexRecord
{
    public int RecordType { get; init; }
    public int Station { get; init; }
    public string BatteryId { get; init; } = string.Empty;
    public DateTimeOffset Timestamp { get; init; }
    public DateTimeOffset ReceivedAt { get; init; }
    public int Value { get; init; }
    public string ParamBlock { get; init; } = string.Empty;
    public int? BatteryTypeCode { get; init; }
    public int? CapacityMah { get; init; }
    public int? Cycles { get; init; }
    public int? HealthPct { get; init; }
    public string StatusCode { get; init; } = string.Empty;
    public string RawLine { get; init; } = string.Empty;
}
