namespace CBAD.Models;

internal sealed class CadexRecord
{
    public int AnalyzerId          { get; init; }
    public int Station             { get; init; }
    public string BatteryId        { get; init; } = string.Empty;
    public DateTimeOffset Timestamp   { get; init; }
    public DateTimeOffset ReceivedAt  { get; init; }
    public int EventCode           { get; init; }
    public string ParamBlock       { get; init; } = string.Empty;

    // 4-field param block
    public int? ProcessCode        { get; init; }
    public int? VoltageMv          { get; init; }
    public int? CurrentMa          { get; init; }   // negative = discharge
    public int? TemperatureC       { get; init; }

    // 2-field pre-test param block
    public int? TargetCapacityPct  { get; init; }

    // Trailing capacity payload from Normal Processing (event 250) field 8
    // Format examples: "0", "1\2", "2\2" (measured\cycle or similar Cadex encoding)
    public string? CapacityPayload { get; init; }

    // Health field
    public string HealthField      { get; init; } = string.Empty;
    public int? HealthCurrent      { get; init; }
    public int? HealthPrevious     { get; init; }

    // OhmTest field 8
    public int? ResistanceMOhm     { get; init; }

    public string RawLine          { get; init; } = string.Empty;
}
